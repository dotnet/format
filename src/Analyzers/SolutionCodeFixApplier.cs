﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class SolutionCodeFixApplier : ICodeFixApplier
    {
        public async Task<Solution> ApplyCodeFixesAsync(
            Solution solution,
            CodeAnalysisResult result,
            CodeFixProvider codeFix,
            string diagnosticId,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var fixAllProvider = codeFix.GetFixAllProvider();
            if (fixAllProvider?.GetSupportedFixAllScopes()?.Contains(FixAllScope.Solution) != true)
            {
                logger.LogWarning(Resources.Unable_to_fix_0_Code_fix_1_doesnt_support_Fix_All_in_Solution, diagnosticId, codeFix.GetType().Name);
                return solution;
            }

            var document = result.Diagnostics
                .SelectMany(kvp => kvp.Value)
                .Select(diagnostic => solution.GetDocument(diagnostic.Location.SourceTree))
                .FirstOrDefault();

            if (document is null)
            {
                return solution;
            }

            var fixAllContext = new FixAllContext(
                document: document,
                codeFixProvider: codeFix,
                scope: FixAllScope.Solution,
                codeActionEquivalenceKey: null!, // FixAllState supports null equivalence key. This should still be supported.
                diagnosticIds: new[] { diagnosticId },
                fixAllDiagnosticProvider: new DiagnosticProvider(result),
                cancellationToken: cancellationToken);

            try
            {
                var action = await fixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
                if (action is null)
                {
                    logger.LogWarning(Resources.Unable_to_fix_0_Code_fix_1_didnt_return_a_Fix_All_action, diagnosticId, codeFix.GetType().Name);
                    return solution;
                }

                var operations = await action.GetOperationsAsync(cancellationToken).ConfigureAwait(false);
                var applyChangesOperation = operations.OfType<ApplyChangesOperation>().SingleOrDefault();
                if (action is null)
                {
                    logger.LogWarning(Resources.Unable_to_fix_0_Code_fix_1_returned_an_unexpected_operation, diagnosticId, codeFix.GetType().Name);
                    return solution;
                }

                return applyChangesOperation.ChangedSolution;
            }
            catch (Exception ex)
            {
                logger.LogWarning(Resources.Failed_to_apply_code_fix_0_for_1_2, codeFix?.GetType().Name, diagnosticId, ex.Message);
                return solution;
            }
        }

        private class DiagnosticProvider : FixAllContext.DiagnosticProvider
        {
            private static Task<IEnumerable<Diagnostic>> EmptyDignosticResult => Task.FromResult(Enumerable.Empty<Diagnostic>());
            private readonly IReadOnlyDictionary<Project, List<Diagnostic>> _diagnosticsByProject;

            internal DiagnosticProvider(CodeAnalysisResult analysisResult)
            {
                _diagnosticsByProject = analysisResult.Diagnostics;
            }

            public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return GetProjectDiagnosticsAsync(project, cancellationToken);
            }

            public override Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken)
            {
                return _diagnosticsByProject.ContainsKey(project)
                    ? Task.FromResult<IEnumerable<Diagnostic>>(_diagnosticsByProject[project])
                    : EmptyDignosticResult;
            }
        }
    }
}
