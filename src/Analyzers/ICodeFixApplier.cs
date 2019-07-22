using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface ICodeFixApplier
    {
        Task<Solution> ApplyCodeFixesAsync(Solution solution, CodeAnalysisResult result, System.Collections.Immutable.ImmutableArray<(Document, OptionSet, ICodingConventionsSnapshot)> formattableDocuments, ILogger logger, CancellationToken cancellationToken);
    }
}
