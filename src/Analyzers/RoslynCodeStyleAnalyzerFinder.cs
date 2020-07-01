﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class RoslynCodeStyleAnalyzerFinder : IAnalyzerFinder
    {
        private static readonly string s_executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private readonly string _featuresPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.Features.dll");
        private readonly string _featuresCSharpPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.Features.dll");
        private readonly string _featuresVisualBasicPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.VisualBasic.Features.dll");

        public ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions options,
            ILogger logger)
        {
            if (!options.FixCodeStyle)
            {
                return ImmutableArray<(DiagnosticAnalyzer Analyzer, CodeFixProvider? Fixer)>.Empty;
            }

            var assemblies = new[]
            {
                _featuresPath,
                _featuresCSharpPath,
                _featuresVisualBasicPath
            }.Select(path => Assembly.LoadFrom(path));

            return AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies, logger);
        }

        public async Task<(DiagnosticSeverity, ImmutableDictionary<Project, ImmutableArray<DiagnosticAnalyzer>>)> FilterBySeverityAsync(
            IEnumerable<Project> projects,
            ImmutableArray<DiagnosticAnalyzer> allAnalyzers,
            ImmutableHashSet<string> formattablePaths,
            FormatOptions formatOptions,
            CancellationToken cancellationToken)
        {
            return (formatOptions.CodeStyleSeverity, await AnalyzerFinderHelpers.FilterBySeverityAsync(projects, allAnalyzers, formattablePaths, formatOptions.CodeStyleSeverity, cancellationToken));
        }
    }
}
