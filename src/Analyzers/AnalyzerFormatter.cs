// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

# nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.ExternalAccess.Format;
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
            if (!options.FormatType.HasFlag(FormatType.CodeStyle))
            {
                return solution;
            }


            var analyzers = await _finder.FindAllAnalyzersAsync(logger, cancellationToken);
            var codefixes = await _finder.FindAllCodeFixesAsync(logger, cancellationToken);
            var paths = formattableDocuments.Select(x => x.Item1.FilePath).ToImmutableArray();
            foreach (var (analyzer, codefix) in pairs)
            {
                var result = new CodeAnalysisResult();
                await solution.Projects.ForEachAsync(async (project, token) =>
                {
                    var options = CodeStyleAnalyzers.GetWorkspaceAnalyzerOptions(project);
                    await _runner.RunCodeAnalysisAsync(result, analyzer, project, options, paths, logger, token);
                }, cancellationToken);

                solution = await _applier.ApplyCodeFixesAsync(solution, result, codefix, logger, cancellationToken);
            }

            return solution;
        }
    }
}
