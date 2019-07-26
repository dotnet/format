// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Immutable;

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.ExternalAccess.Format;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class RoslynCodeStyleAnalyzerFinder : IAnalyzerFinder
    {
        public ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider Fixer)> GetAnalyzersAndFixers() => CodeStyleAnalyzers.GetAnalyzersAndFixers();
        public AnalyzerOptions GetWorkspaceAnalyzerOptions(Project project) => CodeStyleAnalyzers.GetWorkspaceAnalyzerOptions(project);
    }
}
