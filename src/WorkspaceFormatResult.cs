// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Tools
{
    internal record WorkspaceFormatResult(
        int ExitCode,
        int FilesFormatted,
        int FileCount);
}
