// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal partial class ConcurrentAnalyzerRunner : IAnalyzerRunner
    {
        public static IAnalyzerRunner Instance { get; } = new ConcurrentAnalyzerRunner();

        public async Task RunCodeAnalysisAsync(CodeAnalysisResult result,
                                               DiagnosticAnalyzer analyzers,
                                               Project project,
                                               AnalyzerOptions analyzerOptions,
                                               ImmutableArray<string> formattableDocumentPaths,
                                               ILogger logger,
                                               CancellationToken cancellationToken)
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);
            var analyzerCompilation = compilation.WithAnalyzers(
                ImmutableArray.Create(analyzers),
                options: analyzerOptions,
                cancellationToken);
            var diagnostics = await analyzerCompilation.GetAllDiagnosticsAsync(cancellationToken);
            // filter diagnostics
            var filteredDiagnostics = diagnostics.Where(
                x => !x.IsSuppressed &&
                     x.Severity >= DiagnosticSeverity.Warning &&
                     x.Location.IsInSource &&
                     formattableDocumentPaths.Contains(x.Location.SourceTree.FilePath, StringComparer.OrdinalIgnoreCase));
            result.AddDiagnostic(project, filteredDiagnostics);
        }
    }
}
