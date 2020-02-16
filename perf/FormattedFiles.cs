﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [SimpleJob(RuntimeMoniker.NetCoreApp21)]
    public class FormattedFiles
    {
        private const string UnformattedProjectPath = "tests/projects/for_code_formatter/unformatted_project";
        private const string UnformattedProjectFilePath = UnformattedProjectPath + "/unformatted_project.csproj";
        private const string UnformattedSolutionFilePath = "tests/projects/for_code_formatter/unformatted_solution/unformatted_solution.sln";
        private static readonly EmptyLogger EmptyLogger = new EmptyLogger();
        private static readonly Matcher FileMatcher = new Matcher();

        [IterationSetup]
        public void NoFilesFormattedSetup()
        {
            MSBuildRegister.RegisterInstance();
            SolutionPathSetter.SetCurrentDirectory();
        }

        [Benchmark(Description = "Whitespace Formatting (folder)")]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectPath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                FileMatcher,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Whitespace Formatting (project)")]
        public void FilesFormattedProject()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedProjectFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                FileMatcher,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [Benchmark(Description = "Whitespace Formatting (solution)")]
        public void FilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedSolutionFilePath);
            var options = new FormatOptions(
                workspacePath,
                workspaceType,
                LogLevel.Error,
                saveFormattedFiles: false,
                changesAreErrors: false,
                FileMatcher,
                reportPath: string.Empty);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void NoFilesFormattedCleanup() => SolutionPathSetter.UnsetCurrentDirectory();
    }
}
