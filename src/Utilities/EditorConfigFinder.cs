// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class EditorConfigFinder
    {
        public static ImmutableArray<string> GetEditorConfigPaths(ImmutableArray<string> paths)
        {
            if (paths == default)
            {
                return ImmutableArray<string>.Empty;
            }

            var editorConfigPaths = ImmutableArray.CreateBuilder<string>(16);
            var visited = new List<string>();

            foreach (var path in paths)
            {
                var directoryName = Path.GetDirectoryName(path);
                if (visited.Contains(directoryName)) continue;

                var directory = new DirectoryInfo(directoryName);

                // Walk from the folder path up to the drive root addings .editorconfig files.
                while (directory.Parent != null)
                {
                    if (visited.Contains(directory.FullName)) break;

                    visited.Add(directory.FullName);

                    var file = Path.Combine(directory.FullName, ".editorconfig");
                    if (File.Exists(file)) editorConfigPaths.Add(file);

                    directory = directory.Parent;
                }
            }

            return editorConfigPaths.ToImmutable();
        }
    }
}
