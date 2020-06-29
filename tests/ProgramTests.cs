﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using Microsoft.Extensions.Logging;

using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests
{
    public class ProgramTests
    {
        // Should be kept in sync with Program.Run
        private delegate void TestCommandHandlerDelegate(
            string workspace,
            bool folder,
            string verbosity,
            bool check,
            string[] include,
            string[] exclude,
            string report,
            bool includeGenerated);

        [Fact]
        public void ExitCodeIsOneWithCheckAndAnyFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 1, fileCount: 0, exitCode: 0);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(Program.CheckFailedExitCode, exitCode);
        }

        [Fact]
        public void ExitCodeIsZeroWithCheckAndNoFilesFormatted()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
            var exitCode = Program.GetExitCode(formatResult, check: true);

            Assert.Equal(0, exitCode);
        }

        [Fact]
        public void ExitCodeIsSameWithoutCheck()
        {
            var formatResult = new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 42);
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
            Assert.Equal(LogLevel.Information, result.ValueForOption<LogLevel>("verbosity"));
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
            Assert.Equal(LogLevel.Information, result.ValueForOption<LogLevel>("verbosity"));
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
            Assert.Equal(LogLevel.Information, result.ValueForOption<LogLevel>("verbosity"));
        }

        [Fact]
        public void CommandLine_ProjectArgument_GetsPassedToHandler()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();
            var handlerWasCalled = false;
            sut.Handler = CommandHandler.Create(new TestCommandHandlerDelegate(TestCommandHandler));

            void TestCommandHandler(
                string workspace,
                bool folder,
                string verbosity,
                bool check,
                string[] include,
                string[] exclude,
                string report,
                bool includeGenerated)
            {
                handlerWasCalled = true;
                Assert.Equal("workspaceValue", workspace);
                Assert.Equal("detailed", verbosity);
            };

            // Act
            var result = sut.Invoke(new[] { "--verbosity", "detailed", "workspace" });

            // Assert
            Assert.True(handlerWasCalled);
        }

        [Fact]
        public void CommandLine_ProjectArgument_FailesIfSpecifiedTwice()
        {
            // Arrange
            var sut = FormatCommand.CreateCommandLineOptions();

            // Act
            var result = sut.Parse(new[] { "workspaceValue1", "workspaceValue2" });

            // Assert
            Assert.Equal(1, result.Errors.Count);
        }
    }
}
