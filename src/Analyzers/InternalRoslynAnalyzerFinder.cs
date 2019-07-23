// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.Format;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class InternalRoslynAnalyzerFinder : IAnalyzerFinder
    {
        public static IAnalyzerFinder Instance { get; } = new InternalRoslynAnalyzerFinder();

        public Task<ImmutableArray<DiagnosticAnalyzer>> FindAllAnalyzersAsync(ILogger logger, CancellationToken cancellationToken)
        {
            // The `GetAnalyzersAsync` will be fixed in a forthcoming build of ExternalAccess.Format
            return Task.FromResult(CodeStyleAnalyzers.GetAnalyzersAsync());
        }

        public Task<ImmutableArray<CodeFixProvider>> FindAllCodeFixesAsync(ILogger logger, CancellationToken cancellationToken)
        {
            // Code fix providers will be in a forthcoming build of ExternalAccess.Format
            return Task.FromResult(new ImmutableArray<CodeFixProvider>());
        }
    }
}
