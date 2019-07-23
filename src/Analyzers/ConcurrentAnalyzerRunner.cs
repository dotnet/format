// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.Format;
using Microsoft.CodeAnalysis.Options;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal partial class ConcurrentAnalyzerRunner : IAnalyzerRunner
    {
        public static IAnalyzerRunner Instance { get; } = new ConcurrentAnalyzerRunner();

        public async Task<CodeAnalysisResult> RunCodeAnalysisAsync(DiagnosticAnalyzer analyzers,
                                                                   Project project,
                                                                   AnalyzerOptions analyzerOptions,
                                                                   ILogger logger,
                                                                   CancellationToken cancellationToken)
        {
            var result = new CodeAnalysisResult();
            var solution = formattableDocuments.FirstOrDefault().Document.Project.Solution;
            var documents = formattableDocuments.Select(x => x.Document).ToList();

            await formattableDocuments.ForEachAsync(async (tuple, token) =>
            {
                var compilation = project.GetCompilationAsync(cancellationToken).GetAwaiter().GetResult();
                // TODO: generate option set to ensure the analyzers run
                // TODO: Ensure that the coding conventions snapshop gets passed to the analyzers somehow
                var workspaceAnalyzerOptions = CodeStyleAnalyzers.GetWorkspaceAnalyzerOptions(project);
                var analyzerCompilation = compilation.WithAnalyzers(analyzers, workspaceAnalyzerOptions, cancellationToken);
                var diagnostics = analyzerCompilation.GetAllDiagnosticsAsync(cancellationToken).GetAwaiter().GetResult();
                result.AddDiagnostic(project, diagnostics);
            });

            return Task.FromResult(result);
        }
    }
}
