// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class FormatOptions
    {
        public string WorkspaceFilePath { get; }
        public WorkspaceType WorkspaceType { get; }
        public LogLevel LogLevel { get; }
        public FormatType FormatType { get; }
        public bool SaveFormattedFiles { get; }
        public bool ChangesAreErrors { get; }
        public ImmutableHashSet<string> FilesToFormat { get; }

        public FormatOptions(
            string workspaceFilePath,
            WorkspaceType workspaceType,
            LogLevel logLevel,
            FormatType formatType,
            bool saveFormattedFiles,
            bool changesAreErrors,
            ImmutableHashSet<string> filesToFormat)
        {
            WorkspaceFilePath = workspaceFilePath;
            WorkspaceType = workspaceType;
            LogLevel = logLevel;
            FormatType = formatType;
            SaveFormattedFiles = saveFormattedFiles;
            ChangesAreErrors = changesAreErrors;
            FilesToFormat = filesToFormat;
        }

        public void Deconstruct(
            out string workspaceFilePath,
            out WorkspaceType workspaceType,
            out LogLevel logLevel,
            out FormatType formatType,
            out bool saveFormattedFiles,
            out bool changesAreErrors,
            out ImmutableHashSet<string> filesToFormat)
        {
            workspaceFilePath = WorkspaceFilePath;
            workspaceType = WorkspaceType;
            logLevel = LogLevel;
            formatType = FormatType;
            saveFormattedFiles = SaveFormattedFiles;
            changesAreErrors = ChangesAreErrors;
            filesToFormat = FilesToFormat;
        }
    }
}
