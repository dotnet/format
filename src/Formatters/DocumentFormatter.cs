// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// Base class for code formatters that work against a single document at a time.
    /// </summary>
    internal abstract class DocumentFormatter : ICodeFormatter
    {
        protected abstract string FormatWarningDescription { get;  }

        /// <summary>
        /// Applies formatting and returns a formatted <see cref="Solution"/>
        /// </summary>
        public async Task<Solution> FormatAsync(
            Solution solution,
            ImmutableArray<(Document, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
            FormatOptions options,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = FormatFiles(formattableDocuments, logger, cancellationToken);
            return await ApplyFileChangesAsync(solution, formattedDocuments, options, logger, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        protected abstract Task<SourceText> FormatFileAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            ILogger logger,
            CancellationToken cancellationToken);

        /// <summary>
        /// Applies formatting and returns the changed <see cref="SourceText"/> for each <see cref="Document"/>.
        /// </summary>
        private ImmutableArray<(Document, Task<(SourceText originalText, SourceText formattedText)>)> FormatFiles(
            ImmutableArray<(Document, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedDocuments = ImmutableArray.CreateBuilder<(Document, Task<(SourceText originalText, SourceText formattedText)>)>(formattableDocuments.Length);

            foreach (var (document, options, codingConventions) in formattableDocuments)
            {
                var formatTask = Task.Run(async () => await GetFormattedSourceTextAsync(document, options, codingConventions, logger, cancellationToken).ConfigureAwait(false), cancellationToken);

                formattedDocuments.Add((document, formatTask));
            }

            return formattedDocuments.ToImmutableArray();
        }

        /// <summary>
        /// Get formatted <see cref="SourceText"/> for a <see cref="Document"/>.
        /// </summary>
        private async Task<(SourceText originalText, SourceText formattedText)> GetFormattedSourceTextAsync(
            Document document,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogTrace(Resources.Formatting_code_file_0, Path.GetFileName(document.FilePath));

            var originalSourceText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            var formattedSourceText = await FormatFileAsync(document, options, codingConventions, logger, cancellationToken).ConfigureAwait(false);

            return !formattedSourceText.ContentEquals(originalSourceText)
                ? (originalSourceText, formattedSourceText)
                : (originalSourceText, null);
        }

        /// <summary>
        /// Applies the changed <see cref="SourceText"/> to each formatted <see cref="Document"/>.
        /// </summary>
        private async Task<Solution> ApplyFileChangesAsync(
            Solution solution,
            ImmutableArray<(Document, Task<(SourceText originalText, SourceText formattedText)>)> formattedDocuments,
            FormatOptions options,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var formattedSolution = solution;

            foreach (var (document, formatTask) in formattedDocuments)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return formattedSolution;
                }

                var (originalText, formattedText) = await formatTask.ConfigureAwait(false);
                if (formattedText is null)
                {
                    continue;
                }

                if (options.LogLevel == LogLevel.Trace)
                {
                    LogFormattingChanges(options.WorkspaceFilePath, document.FilePath, originalText, formattedText, logger);
                }

                formattedSolution = formattedSolution.WithDocumentText(document.Id, formattedText);
            }

            return formattedSolution;
        }

        private void LogFormattingChanges(string workspacePath, string filePath, SourceText originalText, SourceText formattedText, ILogger logger)
        {
            var workspaceFolder = Path.GetDirectoryName(workspacePath);
            var changes = formattedText.GetChangeRanges(originalText);
            foreach (var change in changes)
            {
                var changePosition = originalText.Lines.GetLinePosition(change.Span.Start);
                logger.LogTrace($"{Path.GetRelativePath(workspaceFolder, filePath)}({changePosition.Line},{changePosition.Character}): {FormatWarningDescription}");
            }
        }
    }
}
