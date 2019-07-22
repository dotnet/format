using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface IAnalyzerFinder
    {
        Task<ImmutableArray<DiagnosticAnalyzer>> FindAllAnalyzersAsync(ILogger logger, CancellationToken cancellationToken);
    }
}
