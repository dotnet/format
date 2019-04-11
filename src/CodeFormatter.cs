﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools
{
    internal static class CodeFormatter
    {
        private static readonly ImmutableArray<ICodeFormatter> s_codeFormatters = new ICodeFormatter[]
        {
            new WhitespaceFormatter()
        }.ToImmutableArray();

        public static async Task<WorkspaceFormatResult> FormatWorkspaceAsync(
            string solutionOrProjectPath,
            bool isSolution,
            bool logWorkspaceWarnings,
            bool saveFormattedFiles,
            ImmutableHashSet<string> filesToFormat,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogInformation(string.Format(Resources.Formatting_code_files_in_workspace_0, solutionOrProjectPath));

            logger.LogTrace(Resources.Loading_workspace);

            var workspaceStopwatch = Stopwatch.StartNew();

            using (var workspace = await OpenWorkspaceAsync(
                solutionOrProjectPath, isSolution, logWorkspaceWarnings, logger, cancellationToken).ConfigureAwait(false))
            {
                if (workspace is null)
                {
                    return new WorkspaceFormatResult(filesFormatted: 0, fileCount: 0, exitCode: 1);
                }

                var loadWorkspaceMS = workspaceStopwatch.ElapsedMilliseconds;
                logger.LogTrace(Resources.Complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

                var projectPath = isSolution ? string.Empty : solutionOrProjectPath;
                var solution = workspace.CurrentSolution;

                logger.LogTrace(Resources.Determining_formattable_files);

                var (fileCount, formatableFiles) = await DetermineFormattableFiles(
                    solution, projectPath, filesToFormat, logger, cancellationToken).ConfigureAwait(false);

                var determineFilesMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS;
                logger.LogTrace(Resources.Complete_in_0_ms, determineFilesMS);

                logger.LogTrace(Resources.Running_formatters);

                var formattedSolution = await RunCodeFormattersAsync(
                    solution, formatableFiles, logger, cancellationToken).ConfigureAwait(false);

                var formatterRanMS = workspaceStopwatch.ElapsedMilliseconds - loadWorkspaceMS - determineFilesMS;
                logger.LogTrace(Resources.Complete_in_0_ms, formatterRanMS);

                var solutionChanges = formattedSolution.GetChanges(solution);

                var filesFormatted = 0;
                foreach (var projectChanges in solutionChanges.GetProjectChanges())
                {
                    foreach (var changedDocumentId in projectChanges.GetChangedDocuments())
                    {
                        var changedDocument = solution.GetDocument(changedDocumentId);
                        logger.LogInformation(Resources.Formatted_code_file_0, Path.GetFileName(changedDocument.FilePath));
                        filesFormatted++;
                    }
                }

                var exitCode = 0;

                if (saveFormattedFiles && !workspace.TryApplyChanges(formattedSolution))
                {
                    logger.LogError(Resources.Failed_to_save_formatting_changes);
                    exitCode = 1;
                }

                logger.LogDebug(Resources.Formatted_0_of_1_files, filesFormatted, fileCount);

                logger.LogInformation(Resources.Format_complete_in_0_ms, workspaceStopwatch.ElapsedMilliseconds);

                return new WorkspaceFormatResult(filesFormatted, fileCount, exitCode);
            }
        }

        private static async Task<Workspace> OpenWorkspaceAsync(
            string solutionOrProjectPath,
            bool isSolution,
            bool logWorkspaceWarnings,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // This property ensures that XAML files will be compiled in the current AppDomain
                // rather than a separate one. Any tasks isolated in AppDomains or tasks that create
                // AppDomains will likely not work due to https://github.com/Microsoft/MSBuildLocator/issues/16.
                { "AlwaysCompileMarkupFilesInSeparateDomain", bool.FalseString },
                // This flag is used at restore time to avoid imports from packages changing the inputs to restore,
                // without this it is possible to get different results between the first and second restore.
                { "ExcludeRestorePackageImports", bool.TrueString },
            };

            var workspace = MSBuildWorkspace.Create(properties);
            workspace.WorkspaceFailed += LogWorkspaceWarnings;

            var projectPath = string.Empty;
            if (isSolution)
            {
                await workspace.OpenSolutionAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    await workspace.OpenProjectAsync(solutionOrProjectPath, cancellationToken: cancellationToken).ConfigureAwait(false);
                    projectPath = solutionOrProjectPath;
                }
                catch (InvalidOperationException)
                {
                    logger.LogError(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, solutionOrProjectPath);
                    workspace.Dispose();
                    return null;
                }
            }

            workspace.WorkspaceFailed -= LogWorkspaceWarnings;

            return workspace;

            void LogWorkspaceWarnings(object sender, WorkspaceDiagnosticEventArgs args)
            {
                if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    return;
                }

                if (!logWorkspaceWarnings)
                {
                    logger.LogWarning(Resources.Warnings_were_encountered_while_loading_the_workspace_Set_the_verbosity_option_to_the_diagnostic_level_to_log_warnings);
                    ((MSBuildWorkspace)sender).WorkspaceFailed -= LogWorkspaceWarnings;
                    return;
                }

                logger.LogWarning(args.Diagnostic.Message);
            }
        }

        private static async Task<Solution> RunCodeFormattersAsync(
            Solution solution,
            ImmutableArray<(Document, OptionSet)> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var codeFormatter in s_codeFormatters)
            {
                formattedSolution = await codeFormatter.FormatAsync(formattedSolution, formattableDocuments, logger, cancellationToken).ConfigureAwait(false);
            }

            return formattedSolution;
        }

        internal static async Task<(int, ImmutableArray<(Document, OptionSet)>)> DetermineFormattableFiles(
            Solution solution,
            string projectPath,
            ImmutableHashSet<string> filesToFormat,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var codingConventionsManager = CodingConventionsManagerFactory.CreateCodingConventionsManager();
            var optionsApplier = new EditorConfigOptionsApplier();

            var fileCount = 0;
            var getDocumentsAndOptions = new List<Task<(Document, OptionSet, bool)>>(solution.Projects.Sum(project => project.DocumentIds.Count));

            foreach (var project in solution.Projects)
            {
                // If a project is used as a workspace, then ignore other referenced projects.
                if (!string.IsNullOrEmpty(projectPath) && !project.FilePath.Equals(projectPath, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug(Resources.Skipping_referenced_project_0, project.Name);
                    continue;
                }

                // Ignore unsupported project types.
                if (project.Language != LanguageNames.CSharp && project.Language != LanguageNames.VisualBasic)
                {
                    logger.LogWarning(Resources.Could_not_format_0_Format_currently_supports_only_CSharp_and_Visual_Basic_projects, project.FilePath);
                    continue;
                }

                fileCount += project.DocumentIds.Count;

                // Get project documents and options with .editorconfig settings applied.
                var getProjectDocuments = project.DocumentIds.Select(documentId => Task.Run(async () => await GetDocumentAndOptions(
                    project, documentId, filesToFormat, codingConventionsManager, optionsApplier, cancellationToken).ConfigureAwait(false), cancellationToken));
                getDocumentsAndOptions.AddRange(getProjectDocuments);
            }

            var documentsAndOptions = await Task.WhenAll(getDocumentsAndOptions).ConfigureAwait(false);
            var foundEditorConfig = documentsAndOptions.Any(documentAndOptions => documentAndOptions.Item3);

            var addedFilePaths = new HashSet<string>(documentsAndOptions.Length);
            var formattableFiles = ImmutableArray.CreateBuilder<(Document, OptionSet)>(documentsAndOptions.Length);
            foreach (var (document, options, hasEditorConfig) in documentsAndOptions)
            {
                if (document is null)
                {
                    continue;
                }

                // If any code file has an .editorconfig, then we should ignore files without an .editorconfig entry.
                if (foundEditorConfig && !hasEditorConfig)
                {
                    continue;
                }

                // If we've already added this document, either via a link or multi-targeted framework, then ignore.
                if (addedFilePaths.Contains(document.FilePath))
                {
                    continue;
                }

                addedFilePaths.Add(document.FilePath);
                formattableFiles.Add((document, options));
            }

            return (fileCount, formattableFiles.ToImmutableArray());
        }

        private static async Task<(Document, OptionSet, bool)> GetDocumentAndOptions(
            Project project,
            DocumentId documentId,
            ImmutableHashSet<string> filesToFormat,
            ICodingConventionsManager codingConventionsManager,
            EditorConfigOptionsApplier optionsApplier,
            CancellationToken cancellationToken)
        {
            var document = project.Solution.GetDocument(documentId);

            // If a files list was passed in, then ignore files not present in the list.
            if (!filesToFormat.IsEmpty && !filesToFormat.Contains(document.FilePath))
            {
                return (null, null, false);
            }

            if (!document.SupportsSyntaxTree)
            {
                return (null, null, false);
            }

            // Ignore generated code files.
            if (await GeneratedCodeUtilities.IsGeneratedCodeAsync(document, cancellationToken).ConfigureAwait(false))
            {
                return (null, null, false);
            }

            var context = await codingConventionsManager.GetConventionContextAsync(
                document.FilePath, cancellationToken).ConfigureAwait(false);

            OptionSet options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);

            // Check whether an .editorconfig was found for this document.
            if (context?.CurrentConventions is null)
            {
                return (document, options, false);
            }

            options = optionsApplier.ApplyConventions(options, context.CurrentConventions, project.Language);
            return (document, options, true);
        }
    }
}
