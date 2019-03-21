﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        private static readonly string[] _verbosityLevels = new[] { "q", "quiet", "m", "minimal", "n", "normal", "d", "detailed", "diag", "diagnostic" };

        private static async Task<int> Main(string[] args)
        {
            var parser = new CommandLineBuilder(new Command("dotnet-format", handler: CommandHandler.Create(typeof(Program).GetMethod(nameof(Run)))))
                .UseParseDirective()
                .UseHelp()
                .UseDebugDirective()
                .UseSuggestDirective()
                .RegisterWithDotnetSuggest()
                .UseParseErrorReporting()
                .UseExceptionHandler()
                .AddOption(new Option(new[] { "-w", "--workspace" }, Resources.The_solution_or_project_file_to_operate_on_If_a_file_is_not_specified_the_command_will_search_the_current_directory_for_one, new Argument<string>(() => null)))
                .AddOption(new Option(new[] { "-v", "--verbosity" }, Resources.Set_the_verbosity_level_Allowed_values_are_quiet_minimal_normal_detailed_and_diagnostic, new Argument<string>() { Arity = ArgumentArity.ExactlyOne }.FromAmong(_verbosityLevels)))
                .AddOption(new Option(new[] { "--dry-run" }, Resources.Format_files_but_do_not_save_changes_to_disk, new Argument<bool>()))
                .AddOption(new Option(new[] { "--check" }, Resources.Terminate_with_non_zero_exit_code_if_any_files_need_to_be_formatted_in_the_workspace, new Argument<bool>()))
                .AddOption(new Option(new[] { "--files" }, Resources.The_files_to_operate_on_If_none_specified_all_files_in_workspace_will_be_operated_on, new Argument<string>(() => null)))
                .UseVersionOption()
                .Build();

            return await parser.InvokeAsync(args).ConfigureAwait(false);
        }

        public static async Task<int> Run(string workspace, string verbosity, bool dryRun, bool check, string files, IConsole console = null)
        {
            var serviceCollection = new ServiceCollection();
            var logLevel = GetLogLevel(verbosity);
            ConfigureServices(serviceCollection, console, logLevel);

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            string currentDirectory = string.Empty;

            try
            {
                currentDirectory = Environment.CurrentDirectory;

                var workingDirectory = Directory.GetCurrentDirectory();
                var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(workingDirectory, workspace);

                // To ensure we get the version of MSBuild packaged with the dotnet SDK used by the
                // workspace, use its directory as our working directory which will take into account
                // a global.json if present.
                var workspaceDirectory = Path.GetDirectoryName(workspacePath);
                Environment.CurrentDirectory = workingDirectory;

                var fileList = GetFileList(files);

                // Since we are running as a dotnet tool we should be able to find an instance of
                // MSBuild in a .NET Core SDK.
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                // Since we do not inherit msbuild.deps.json when referencing the SDK copy
                // of MSBuild and because the SDK no longer ships with version matched assemblies, we
                // register an assembly loader that will load assemblies from the msbuild path with
                // equal or higher version numbers than requested.
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);

                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);

                var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                    logger,
                    workspacePath,
                    isSolution,
                    logAllWorkspaceWarnings: logLevel == LogLevel.Trace,
                    saveFormattedFiles: !dryRun,
                    filesToFormat: fileList,
                    cancellationTokenSource.Token).ConfigureAwait(false);

                return GetExitCode(formatResult, check);
            }
            catch (FileNotFoundException fex)
            {
                logger.LogError(fex.Message);
                return 1;
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
            finally
            {
                if (!string.IsNullOrEmpty(currentDirectory))
                {
                    Environment.CurrentDirectory = currentDirectory;
                }
            }
        }

        public static int GetExitCode(WorkspaceFormatResult formatResult, bool check) =>
            !check ? formatResult.ExitCode : (formatResult.FilesFormatted == 0 ? 0 : 1);

        private static LogLevel GetLogLevel(string verbosity)
        {
            switch (verbosity)
            {
                case "q":
                case "quiet":
                    return LogLevel.Error;
                case "m":
                case "minimal":
                    return LogLevel.Warning;
                case "n":
                case "normal":
                    return LogLevel.Information;
                case "d":
                case "detailed":
                    return LogLevel.Debug;
                case "diag":
                case "diagnostic":
                    return LogLevel.Trace;
                default:
                    return LogLevel.Information;
            }
        }

        private static void ConfigureServices(ServiceCollection serviceCollection, IConsole console, LogLevel logLevel)
        {
            serviceCollection.AddSingleton(new LoggerFactory().AddSimpleConsole(console, logLevel));
            serviceCollection.AddLogging();
        }
        
        private static string[] GetFileList(string files)
        {
            return files?.Split(',').Select(path => Path.GetRelativePath(Environment.CurrentDirectory, path)).ToArray();
        }
    }
}
