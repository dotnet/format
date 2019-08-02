// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using NonBlocking;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public class CodeAnalysisResult
    {
        private readonly ConcurrentDictionary<Project, List<Diagnostic>> _dictionary
            = new ConcurrentDictionary<Project, List<Diagnostic>>();

        internal void AddDiagnostic(Project project, IEnumerable<Diagnostic> diagnostics)
        {
            _ = _dictionary.AddOrUpdate(project,
                addValueFactory: (key) => diagnostics.ToList(),
                updateValueFactory: (key, list) =>
                {
                    list.AddRange(diagnostics);
                    return list;
                });
        }

        public IReadOnlyDictionary<Project, ImmutableArray<Diagnostic>> Diagnostics
            => _dictionary.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());
    }
}
