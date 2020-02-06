// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Composition;
using Microsoft.VisualStudio.CodingConventions;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    [Export(typeof(IFileWatcher)), Shared]
    internal sealed class NullFileWatcher : IFileWatcher
    {
#pragma warning disable CS0414
        public event ConventionsFileChangedAsyncEventHandler ConventionFileChanged;
        public event ContextFileMovedAsyncEventHandler ContextFileMoved;
#pragma warning restore CS0414

        public void Dispose()
        {
            ConventionFileChanged = null;
            ContextFileMoved = null;
        }

        public void StartWatching(string fileName, string directoryPath)
        {
        }

        public void StopWatching(string fileName, string directoryPath)
        {
        }
    }
}
