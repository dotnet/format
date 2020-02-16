﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    [Config(typeof(RealWorldConfig))]
    public class RealWorldSolution
    {
        private const string UnformattedSolutionFilePath = "temp/project-system/ProjectSystem.sln";
        private const string UnformattedFolderFilePath = "temp/project-system";
        private static EmptyLogger EmptyLogger = new EmptyLogger();
        private static readonly Matcher FileMatcher = new Matcher();

        [IterationSetup]
        public void RealWorldSolutionIterationSetup()
        {
            MSBuildRegister.RegisterInstance();
            SolutionPathSetter.SetCurrentDirectory();
        }

        [Benchmark(Description = "Formatting Solution")]
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

        [Benchmark(Description = "Formatting Folder", Baseline = true)]
        public void FilesFormattedFolder()
        {
            var (workspacePath, workspaceType) = WorkspacePathHelper.GetWorkspaceInfo(UnformattedFolderFilePath);
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
        public void RealWorldSolutionCleanup() => SolutionPathSetter.UnsetCurrentDirectory();

        private class RealWorldConfig : ManualConfig
        {
            public RealWorldConfig()
            {
                var job = Job.Dry
                    .With(BenchmarkDotNet.Environments.Platform.X64)
                    .With(CoreRuntime.Core21)
                    .WithWarmupCount(1)
                    .WithIterationCount(12)
                    .WithOutlierMode(BenchmarkDotNet.Mathematics.OutlierMode.RemoveAll);
                Add(DefaultConfig.Instance
                    .With(job.AsDefault())
                    .With(MemoryDiagnoser.Default));
            }
        }
    }
}
