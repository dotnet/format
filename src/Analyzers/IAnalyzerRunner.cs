using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface IAnalyzerRunner
    {
        Task<CodeAnalysisResult> RunCodeAnalysisAsync(ImmutableArray<DiagnosticAnalyzer> analyzers, ImmutableArray<(Document, OptionSet, ICodingConventionsSnapshot)> formattableDocuments, ILogger logger, CancellationToken cancellationToken);
    }
}
