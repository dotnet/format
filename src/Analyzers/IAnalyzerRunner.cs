// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    interface IAnalyzerRunner
    {
        Task<CodeAnalysisResult> RunCodeAnalysisAsync(DiagnosticAnalyzer analyzers,
                                                      Project project,
                                                      AnalyzerOptions analyzerOptions,
                                                      ILogger logger,
                                                      CancellationToken cancellationToken);
    }
}
