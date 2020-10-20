// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.CodeAnalysis.Tools
{
    public record FormattedFile(
        DocumentId DocumentId,
        string FileName,
        string? FilePath,
        IEnumerable<FileChange> FileChanges)
    {
        public FormattedFile(Document document, IEnumerable<FileChange> fileChanges)
            : this(document.Id, document.Name, document.FilePath, fileChanges)
        {
        }
    }
}
