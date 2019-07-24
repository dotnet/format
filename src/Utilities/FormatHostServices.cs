// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.ExternalAccess.Format;

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
            TryLoadNearbyAssembly(typeof(CodeStyleAnalyzers).Assembly.GetName().Name)
        }
        .Concat(MSBuildMefHostServices.DefaultAssemblies)
        .ToImmutableArray();
    }

    private static Assembly TryLoadNearbyAssembly(string assemblySimpleName)
    {
        var thisAssemblyName = typeof(MefHostServices).GetTypeInfo().Assembly.GetName();
        var assemblyShortName = thisAssemblyName.Name;
        var assemblyVersion = thisAssemblyName.Version;
        var publicKeyToken = thisAssemblyName.GetPublicKeyToken().Aggregate(string.Empty, (s, b) => s + b.ToString("x2"));

        if (string.IsNullOrEmpty(publicKeyToken))
        {
            publicKeyToken = "null";
        }

        var assemblyName = new AssemblyName(string.Format("{0}, Version={1}, Culture=neutral, PublicKeyToken={2}", assemblySimpleName, assemblyVersion, publicKeyToken));

        try
        {
            return Assembly.Load(assemblyName);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
