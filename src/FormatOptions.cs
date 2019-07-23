// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class FormatOptions
    {
        public string WorkspaceFilePath { get; }
        public bool IsSolution { get; }
        public LogLevel LogLevel { get; }
        public FormatType FormatType { get; }
        public bool SaveFormattedFiles { get; }
        public bool ChangesAreErrors { get; }
        public ImmutableHashSet<string> FilesToFormat { get; }

        public FormatOptions(
            string workspaceFilePath,
            bool isSolution,
            LogLevel logLevel,
            FormatType formatType,
            bool saveFormattedFiles,
            bool changesAreErrors,
            ImmutableHashSet<string> filesToFormat)
        {
            WorkspaceFilePath = workspaceFilePath;
            IsSolution = isSolution;
            LogLevel = logLevel;
            FormatType = formatType;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FilesToFormat = filesToFormat;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out bool isSolution,
            out LogLevel logLevel,
            out FormatType formatType,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out ImmutableHashSet<string> filesToFormat)
        {
            workspaceFilePath = WorkspaceFilePath;
            isSolution = IsSolution;
            logLevel = LogLevel;
            formatType = FormatType;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            filesToFormat = FilesToFormat;
        }
    }
}
