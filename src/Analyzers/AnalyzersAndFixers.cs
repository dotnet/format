// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal record AnalyzersAndFixers(ImmutableArray<DiagnosticAnalyzer> Analyzers, ImmutableArray<CodeFixProvider> Fixers);
}
