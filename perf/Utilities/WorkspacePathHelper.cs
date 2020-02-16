﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.IO;

namespace Microsoft.CodeAnalysis.Tools.Perf
{
    internal static class WorkspacePathHelper
    {
        internal static (string workspacePath, WorkspaceType workspaceType) GetWorkspaceInfo(string workspaceFilePath)
        {
            var workspacePath = Path.GetFullPath(workspaceFilePath);

            WorkspaceType workspaceType;
            if (Directory.Exists(workspacePath))
            {
                workspaceType = WorkspaceType.Folder;
            }
            else
            {
                workspaceType = workspacePath.EndsWith(".sln")
                    ? WorkspaceType.Solution
                    : WorkspaceType.Project;
            }

            return (workspacePath, workspaceType);
        }
    }
}
