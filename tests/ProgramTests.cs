﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class ProgramTests
    {
        [Fact]
        public void ExitCodeIsOneWithCheckAndAnyFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(FilesFormatted: 1, FileCount: 0, ExitCode: 0);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(Program.CheckFailedExitCode, exitCode);
        }

        [Fact]
        public void ExitCodeIsZeroWithCheckAndNoFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(FilesFormatted: 0, FileCount: 0, ExitCode: 42);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ExitCodeIsSameWithoutCheck()
        {
            var formatResult = new WorkspaceFormatResult(FilesFormatted: 0, FileCount: 0, ExitCode: 42);
            var exitCode = Program.GetExitCode(formatResult, check: false);

            Assert.Equal(formatResult.ExitCode, exitCode);
        }

        [Fact]
        public void CommandLine_OptionsAreParsedCorrectly()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] {
                "--folder",
                "--include", "include1", "include2",
                "--exclude", "exclude1", "exclude2",
                "--check",
                "--report", "report",
                "--verbosity", "detailed",
                "--include-generated"});

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal(0, result.UnmatchedTokens.Count);
            Assert.Equal(0, result.UnparsedTokens.Count);
            Assert.True(result.ValueForOption<bool>("folder"));
            Assert.Collection(result.ValueForOption<IEnumerable<string>>("include"),
                i0 => Assert.Equal("include1", i0),
                i1 => Assert.Equal("include2", i1));
            Assert.Collection(result.ValueForOption<IEnumerable<string>>("exclude"),
                i0 => Assert.Equal("exclude1", i0),
                i1 => Assert.Equal("exclude2", i1));
            Assert.True(result.ValueForOption<bool>("check"));
            Assert.Equal("report", result.ValueForOption("report"));
            Assert.Equal("detailed", result.ValueForOption("verbosity"));
            Assert.True(result.ValueForOption<bool>("include-generated"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_Simple()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "workspaceValue" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("workspaceValue", result.CommandResult.GetArgumentValueOrDefault("workspace"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_WithOption_AfterArgument()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "workspaceValue", "--verbosity", "detailed" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("workspaceValue", result.CommandResult.GetArgumentValueOrDefault("workspace"));
            Assert.Equal("detailed", result.ValueForOption("verbosity"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_WithOption_BeforeArgument()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "--verbosity", "detailed", "workspaceValue" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
            Assert.Equal("workspaceValue", result.CommandResult.GetArgumentValueOrDefault("workspace"));
            Assert.Equal("detailed", result.ValueForOption("verbosity"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_FailsIfSpecifiedTwice()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "workspaceValue1", "workspaceValue2" });

            // Assert
            Assert.Equal(1, result.Errors.Count);
        }

        [Fact]
        public void CommandLine_FolderValidation_FailsIfFixAnalyzersSpecified()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "--folder", "--fix-analyzers" });

            // Assert
            Assert.Equal(1, result.Errors.Count);
        }

        [Fact]
        public void CommandLine_FolderValidation_FailsIfFixStyleSpecified()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "--folder", "--fix-style" });

            // Assert
            Assert.Equal(1, result.Errors.Count);
        }

        [Fact]
        public void CommandLine_AnalyzerOptions_CanSpecifyBothWithDefaults()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "--fix-analyzers", "--fix-style" });

            // Assert
            Assert.Equal(0, result.Errors.Count);
        }

        [Fact]
        // If this test fails that means FormatCommand options have changed, ensure the FormatCommand.Handler has been updated to match.
        public async Task CommandLine_AllArguments_Bind()
        {
            // Arrange
            var uniqueExitCode = 143;

            var sut = FormatCommand.CreateCommandLineOptions();
            sut.Handler = CommandHandler.Create(new FormatCommand.Handler(TestRun));

            Task<int> TestRun(
                string workspace,
                bool folder,
                bool fixWhitespace,
                string fixStyle,
                string fixAnalyzers,
                string verbosity,
                bool check,
                string[] include,
                string[] exclude,
                string report,
                bool includeGenerated,
                IConsole console = null)
            {
                Assert.Equal("./src", workspace);
                Assert.False(folder);
                Assert.True(fixWhitespace);
                Assert.Equal("warn", fixStyle);
                Assert.Equal("info", fixAnalyzers);
                Assert.Equal("diag", verbosity);
                Assert.True(check);
                Assert.Equal(new[] { "*.cs" }, include);
                Assert.Equal(new[] { "*.vb" }, exclude);
                Assert.Equal("report.json", report);
                Assert.True(includeGenerated);

                return Task.FromResult(uniqueExitCode);
            };

            var args = @"
./src
--fix-whitespace
--fix-style
warn
--fix-analyzers
info
--verbosity
diag
--check
--include
*.cs
--exclude
*.vb
--report
report.json
--include-generated".Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            // Act
            var parseResult = sut.Parse(args);
            var result = await sut.InvokeAsync(args);

            // Assert
            Assert.Equal(0, parseResult.Errors.Count);
            Assert.Equal(uniqueExitCode, result);
        }
    }
}
