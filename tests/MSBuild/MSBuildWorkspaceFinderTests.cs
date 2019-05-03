﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.MSBuild
{
    public class MSBuildWorkspaceFinderTests : IClassFixture<SolutionPathFixture>
    {
        private string SolutionPath => Environment.CurrentDirectory;

        public MSBuildWorkspaceFinderTests(SolutionPathFixture solutionPathFixture)
        {
            solutionPathFixture.SetCurrentDirectory();
        }

        [Fact]
        public void ThrowsException_CannotFindMSBuildProjectFile()
        {
            var workspacePath = "tests/projects/for_workspace_finder/no_project_or_solution/";
            var exceptionMessageStart = string.Format(
                Resources.Could_not_find_a_MSBuild_project_or_solution_file_in_0_Specify_which_to_use_with_the_workspace_option,
                Path.Combine(SolutionPath, workspacePath)).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, workspacePath));
            Assert.StartsWith(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_MultipleMSBuildProjectFiles()
        {
            var workspacePath = "tests/projects/for_workspace_finder/multiple_projects/";
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_project_files_found_in_0_Specify_which_to_use_with_the_workspace_option,
                Path.Combine(SolutionPath, workspacePath)).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, workspacePath));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_MultipleMSBuildSolutionFiles()
        {
            var workspacePath = "tests/projects/for_workspace_finder/multiple_solutions/";
            var exceptionMessageStart = string.Format(
                Resources.Multiple_MSBuild_solution_files_found_in_0_Specify_which_to_use_with_the_workspace_option,
                Path.Combine(SolutionPath, workspacePath)).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, workspacePath));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void ThrowsException_SolutionAndProjectAmbiguity()
        {
            var workspacePath = "tests/projects/for_workspace_finder/project_and_solution/";
            var exceptionMessageStart = string.Format(
                Resources.Both_a_MSBuild_project_file_and_solution_file_found_in_0_Specify_which_to_use_with_the_workspace_option,
                Path.Combine(SolutionPath, workspacePath)).Replace('/', Path.DirectorySeparatorChar);
            var exception = Assert.Throws<FileNotFoundException>(() => MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, workspacePath));
            Assert.Equal(exceptionMessageStart, exception.Message);
        }

        [Fact]
        public void FindsSolutionByFolder()
        {
            const string Path = "tests/projects/for_workspace_finder/single_solution/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_solution.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsSolutionByFilePath()
        {
            const string Path = "tests/projects/for_workspace_finder/multiple_solutions/solution_b.sln";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("solution_b.sln", solutionFileName);
            Assert.True(isSolution);
        }

        [Fact]
        public void FindsProjectByFolder()
        {
            const string Path = "tests/projects/for_workspace_finder/single_project/";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("single_project.csproj", solutionFileName);
            Assert.False(isSolution);
        }

        [Fact]
        public void FindsProjectByFilePath()
        {
            const string Path = "tests/projects/for_workspace_finder/multiple_projects/project_b.csproj";

            var (isSolution, workspacePath) = MSBuildWorkspaceFinder.FindWorkspace(SolutionPath, Path);

            var solutionFileName = System.IO.Path.GetFileName(workspacePath);
            Assert.Equal("project_b.csproj", solutionFileName);
            Assert.False(isSolution);
        }
    }
}
