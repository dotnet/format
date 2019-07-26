// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Format;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Formatters
{
    /// <summary>
    /// CodeFormatter that uses the <see cref="ImportsOrganizer"/> to format document imports.
    /// </summary>
    internal sealed class ImportsFormatter : DocumentFormatter
    {
        public override FormatType FormatType => FormatType.CodeStyle;
        protected override string FormatWarningDescription => Resources.Fix_imports_ordering;
        private readonly DocumentFormatter _endOfLineFormatter = new EndOfLineFormatter();

        internal override async Task<SourceText> FormatFileAsync(
            Document document,
            SourceText sourceText,
            OptionSet options,
            ICodingConventionsSnapshot codingConventions,
            FormatOptions formatOptions,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var organizedDocument = await ImportsOrganizer.OrganizeImportsAsync(document, cancellationToken);
            if (organizedDocument == document)
            {
                return sourceText;
            }

            var organizedSourceText = await organizedDocument.GetTextAsync(cancellationToken);
            return await _endOfLineFormatter.FormatFileAsync(organizedDocument, organizedSourceText, options, codingConventions, formatOptions, logger, cancellationToken);
        }
    }
}
