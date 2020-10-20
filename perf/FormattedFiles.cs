﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [SimpleJob(RuntimeMoniker.NetCoreApp21)]
    public class FormattedFiles
    {
        private const string UnformattedProjectPath = "tests/projects/for_code_formatter/unformatted_project/";
        private const string UnformattedProjectFilePath = UnformattedProjectPath + "unformatted_project.csproj";
        private const string UnformattedSolutionFilePath = "tests/projects/for_code_formatter/unformatted_solution/unformatted_solution.sln";

        private static EmptyLogger EmptyLogger => new EmptyLogger();
        private static SourceFileMatcher AllFileMatcher => SourceFileMatcher.CreateMatcher(Array.Empty<string>(), Array.Empty<string>());

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
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false);
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
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false);
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
                FixCategory: FixCategory.Whitespace,
                CodeStyleSeverity: DiagnosticSeverity.Error,
                AnalyzerSeverity: DiagnosticSeverity.Error,
                SaveFormattedFiles: false,
                ChangesAreErrors: false,
                AllFileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false);
            _ = CodeFormatter.FormatWorkspaceAsync(options, EmptyLogger, default).GetAwaiter().GetResult();
        }

        [IterationCleanup]
        public void NoFilesFormattedCleanup() => SolutionPathSetter.UnsetCurrentDirectory();
    }
}
