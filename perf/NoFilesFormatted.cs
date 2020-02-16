﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [SimpleJob(RuntimeMoniker.NetCoreApp21)]
    public class NoFilesFormatted
    {
        private const string FormattedProjectPath = "tests/projects/for_code_formatter/formatted_project";
        private const string FormattedProjectFilePath = FormattedProjectPath + "/formatted_project.csproj";
        private const string FormattedSolutionFilePath = "tests/projects/for_code_formatter/formatted_solution/formatted_solution.sln";

        private static readonly EmptyLogger EmptyLogger = new EmptyLogger();
        private static readonly Matcher FileMatcher = new Matcher();

        [IterationSetup]
        public void NoFilesFormattedSetup()
        {
            MSBuildRegister.RegisterInstance();
            SolutionPathSetter.SetCurrentDirectory();
        }

        [Benchmark(Description = "No Files are Formatted (folder)")]
        public void NoFilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedProjectPath);
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

        [Benchmark(Description = "No Files are Formatted (project)")]
        public void NoFilesFormattedProject()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedProjectFilePath);
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

        [Benchmark(Description = "No Files are Formatted (solution)")]
        public void NoFilesFormattedSolution()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(FormattedSolutionFilePath);
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
