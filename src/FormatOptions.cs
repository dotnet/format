// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal record FormatOptions(
        string WorkspaceFilePath,
        WorkspaceType WorkspaceType,
        LogLevel LogLevel,
        FixCategory FixCategory,
        DiagnosticSeverity CodeStyleSeverity,
        DiagnosticSeverity AnalyzerSeverity,
        bool SaveFormattedFiles,
        bool ChangesAreErrors,
        SourceFileMatcher FileMatcher,
        string? ReportPath,
        bool IncludeGeneratedFiles);
}
