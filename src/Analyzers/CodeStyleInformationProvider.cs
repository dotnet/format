// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    internal class CodeStyleInformationProvider : IAnalyzerInformationProvider
    {
        private static readonly string s_executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

        private readonly string _codeStylePath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CodeStyle.dll");
        private readonly string _codeStyleFixesPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CodeStyle.Fixes.dll");
        private readonly string _csharpCodeStylePath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.CodeStyle.dll");
        private readonly string _csharpCodeStyleFixesPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.CSharp.CodeStyle.Fixes.dll");
        private readonly string _visualBasicCodeStylePath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.VisualBasic.CodeStyle.dll");
        private readonly string _visualBasicCodeStyleFixesPath = Path.Combine(s_executingPath, "Microsoft.CodeAnalysis.VisualBasic.CodeStyle.Fixes.dll");

        public ImmutableDictionary<ProjectId, AnalyzersAndFixers> GetAnalyzersAndFixers(
            Solution solution,
            FormatOptions formatOptions,
            ILogger logger)
        {
            var assemblies = new[]
            {
                _codeStylePath,
                _codeStyleFixesPath,
                _csharpCodeStylePath,
                _csharpCodeStyleFixesPath,
                _visualBasicCodeStylePath,
                _visualBasicCodeStyleFixesPath
            }.Select(path => Assembly.LoadFrom(path));

            var analyzersAndFixers = AnalyzerFinderHelpers.LoadAnalyzersAndFixers(assemblies);
            return solution.Projects
                .ToImmutableDictionary(project => project.Id, project => analyzersAndFixers);
        }

        public DiagnosticSeverity GetSeverity(FormatOptions formatOptions) => formatOptions.CodeStyleSeverity;
    }
}
