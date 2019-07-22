using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    class AnalyzerFormatter : ICodeFormatter
    {
        private readonly IAnalyzerFinder _finder;
        private readonly IAnalyzerRunner _runner;
        private readonly ICodeFixApplier _applier;

        public AnalyzerFormatter(IAnalyzerFinder finder,
                                 IAnalyzerRunner runner, 
                                 ICodeFixApplier applier)
        {
            _finder = finder;
            _runner = runner;
            _applier = applier;
        }

        public async Task<Solution> FormatAsync(Solution solution,
                                          ImmutableArray<(Document, OptionSet, ICodingConventionsSnapshot)> formattableDocuments,
                                          FormatOptions options,
                                          ILogger logger,
                                          CancellationToken cancellationToken)
        {
            var analyzers = await _finder.FindAllAnalyzersAsync(logger, cancellationToken);
            var result = await  _runner.RunCodeAnalysisAsync(analyzers, formattableDocuments, logger, cancellationToken);
            return await _applier.ApplyCodeFixesAsync(solution, result, formattableDocuments, logger, cancellationToken);
        }
    }
}
