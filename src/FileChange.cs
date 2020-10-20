// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Tools
{
    public record FileChange(int LineNumber, int CharNumber, string FormatDescription)
    {
        public FileChange(LinePosition changePosition, string formatDescription)
            // LinePosition is zero based so we need to increment to report numbers people expect.
            : this(changePosition.Line + 1, changePosition.Character + 1, formatDescription)
        {
        }
    }
}
