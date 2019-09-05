// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.ExternalAccess.Format;

namespace Microsoft.CodeAnalysis.Tools.Utilities
{
    internal static class FormatHostServices
    {
        private static MefHostServices s_hostServices;
        public static MefHostServices HostServices
        {
            get
            {
                if (s_hostServices == null)
                {
                    s_hostServices = MefHostServices.Create(FormatHostServices.DefaultAssemblies);
                }

                return s_hostServices;
            }
        }

        private static ImmutableArray<Assembly> s_defaultAssemblies;
        public static ImmutableArray<Assembly> DefaultAssemblies
        {
            get
            {
                if (s_defaultAssemblies.IsDefault)
                {
                    s_defaultAssemblies = LoadDefaultAssemblies();
                }

                return s_defaultAssemblies;
            }
        }

        private static ImmutableArray<Assembly> LoadDefaultAssemblies()
        {
            return new Assembly[]
            {
                typeof(NullFileWatcher).Assembly,
                typeof(CodeStyleAnalyzers).Assembly,
            }
            .Concat(MSBuildMefHostServices.DefaultAssemblies)
            .ToImmutableArray();
        }
    }
}
