﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        internal const int UnhandledExceptionExitCode = 1;
        internal const int CheckFailedExitCode = 2;
        internal const int UnableToLocateMSBuildExitCode = 3;
        internal const int UnableToLocateDotNetCliExitCode = 4;

        private static ParseResult? s_parseResult;

        private static async Task<int> Main(string[] args)
        {
            var rootCommand = FormatCommand.CreateCommandLineOptions();
            rootCommand.Handler = CommandHandler.Create(typeof(Program).GetMethod(nameof(Run)));

            // Parse the incoming args so we can give warnings when deprecated options are used.
            s_parseResult = rootCommand.Parse(args);

            return await rootCommand.InvokeAsync(args);
        }

        public static async Task<int> Run(
            string? workspace,
            bool folder,
            DiagnosticSeverity? fixStyle,
            DiagnosticSeverity? fixAnalyzers,
            LogLevel? verbosity,
            bool check,
            string[] include,
            string[] exclude,
            string? report,
            bool includeGenerated,
            IConsole console = null!)
        {
            if (s_parseResult == null)
            {
                return 1;
            }

            // Setup logging.
            var logLevel = verbosity ?? LogLevel.Information;
            var logger = SetupLogging(console, logLevel);

            // Hook so we can cancel and exit when ctrl+c is pressed.
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var currentDirectory = string.Empty;

            try
            {
                currentDirectory = Environment.CurrentDirectory;


                string workspaceDirectory;
                string workspacePath;
                WorkspaceType workspaceType;

                // The folder option means we should treat the project path as a folder path.
                if (folder)
                {
                    // If folder isn't populated, then use the current directory
                    workspacePath = Path.GetFullPath(workspace ?? ".", Environment.CurrentDirectory);
                    workspaceDirectory = workspacePath;
                    workspaceType = WorkspaceType.Folder;
                }
                else
                {
                    var (isSolution, workspaceFilePath) = MSBuildWorkspaceFinder.FindWorkspace(currentDirectory, workspace);

                    workspacePath = workspaceFilePath;
                    workspaceType = isSolution
                        ? WorkspaceType.Solution
                        : WorkspaceType.Project;

                    // To ensure we get the version of MSBuild packaged with the dotnet SDK used by the
                    // workspace, use its directory as our working directory which will take into account
                    // a global.json if present.
                    workspaceDirectory = Path.GetDirectoryName(workspacePath);
                    if (workspaceDirectory is null)
                    {
                        throw new Exception($"Unable to find folder at '{workspacePath}'");
                    }
                }

                // Load MSBuild
                Environment.CurrentDirectory = workspaceDirectory;

                if (!TryGetDotNetCliVersion(out var dotnetVersion))
                {
                    logger.LogError(Resources.Unable_to_locate_dotnet_CLI_Ensure_that_it_is_on_the_PATH);
                    return UnableToLocateDotNetCliExitCode;
                }

                logger.LogTrace(Resources.The_dotnet_CLI_version_is_0, dotnetVersion);

                if (!TryLoadMSBuild(out var msBuildPath))
                {
                    logger.LogError(Resources.Unable_to_locate_MSBuild_Ensure_the_NET_SDK_was_installed_with_the_official_installer);
                    return UnableToLocateMSBuildExitCode;
                }

                logger.LogTrace(Resources.Using_msbuildexe_located_in_0, msBuildPath);

                var fileMatcher = SourceFileMatcher.CreateMatcher(include, exclude);

                var formatOptions = new FormatOptions(
                    workspacePath,
                    workspaceType,
                    logLevel,
                    fixCodeStyle: fixStyle != null,
                    codeStyleSeverity: fixStyle ?? DiagnosticSeverity.Error,
                    fixAnalyzers: fixAnalyzers != null,
                    analyerSeverity: fixAnalyzers ?? DiagnosticSeverity.Error,
                    saveFormattedFiles: !check,
                    changesAreErrors: check,
                    fileMatcher,
                    reportPath: report,
                    includeGenerated);

                var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                    formatOptions,
                    logger,
                    cancellationTokenSource.Token,
                    createBinaryLog: verbosity == LogLevel.Trace).ConfigureAwait(false);

                return GetExitCode(formatResult, check);
            }
            catch (FileNotFoundException fex)
            {
                logger.LogError(fex.Message);
                return UnhandledExceptionExitCode;
            }
            catch (OperationCanceledException)
            {
                return UnhandledExceptionExitCode;
            }
            finally
            {
                if (!string.IsNullOrEmpty(currentDirectory))
                {
                    Environment.CurrentDirectory = currentDirectory;
                }
            }
        }

        internal static int GetExitCode(WorkspaceFormatResult formatResult, bool check)
        {
            if (!check)
            {
                return formatResult.ExitCode;
            }

            return formatResult.FilesFormatted == 0 ? 0 : CheckFailedExitCode;
        }

        private static ILogger<Program> SetupLogging(IConsole console, LogLevel logLevel)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(new LoggerFactory().AddSimpleConsole(console, logLevel));
            serviceCollection.AddLogging();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            return logger;
        }

        private static bool TryGetDotNetCliVersion([NotNullWhen(returnValue: true)] out string? dotnetVersion)
        {
            try
            {
                var processInfo = ProcessRunner.CreateProcess("dotnet", "--version", captureOutput: true, displayWindow: false);
                var versionResult = processInfo.Result.GetAwaiter().GetResult();

                dotnetVersion = versionResult.OutputLines[0].Trim();
                return true;
            }
            catch
            {
                dotnetVersion = null;
                return false;
            }
        }

        private static bool TryLoadMSBuild([NotNullWhen(returnValue: true)] out string? msBuildPath)
        {
            try
            {
                // Since we are running as a dotnet tool we should be able to find an instance of
                // MSBuild in a .NET Core SDK.
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                // Since we do not inherit msbuild.deps.json when referencing the SDK copy
                // of MSBuild and because the SDK no longer ships with version matched assemblies, we
                // register an assembly loader that will load assemblies from the msbuild path with
                // equal or higher version numbers than requested.
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);

                msBuildPath = msBuildInstance.MSBuildPath;
                return true;
            }
            catch
            {
                msBuildPath = null;
                return false;
            }
        }
    }
}
