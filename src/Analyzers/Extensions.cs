// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.Tools.Analyzers
{
    public static class Extensions
    {
        private static Assembly MicrosoftCodeAnalysisCodeStyleAssembly { get; }
        private static Type IDEDiagnosticIdToOptionMappingHelperType { get; }
        private static MethodInfo TryGetMappedOptionsMethod { get; }

        private static Type IEditorConfigStorageLocation2Type { get; }

        static Extensions()
        {
            MicrosoftCodeAnalysisCodeStyleAssembly = Assembly.Load(new AssemblyName("Microsoft.CodeAnalysis.CodeStyle"));
            IDEDiagnosticIdToOptionMappingHelperType = MicrosoftCodeAnalysisCodeStyleAssembly.GetType("Microsoft.CodeAnalysis.Diagnostics.IDEDiagnosticIdToOptionMappingHelper")!;
            TryGetMappedOptionsMethod = IDEDiagnosticIdToOptionMappingHelperType.GetMethod("TryGetMappedOptions", BindingFlags.Static | BindingFlags.Public)!;

            IEditorConfigStorageLocation2Type = MicrosoftCodeAnalysisCodeStyleAssembly.GetType("Microsoft.CodeAnalysis.Options.IEditorConfigStorageLocation2")!;
        }

        public static bool Any(this SolutionChanges solutionChanges)
                => solutionChanges.GetProjectChanges()
                    .Any(x => x.GetChangedDocuments().Any() || x.GetChangedAdditionalDocuments().Any());

        public static bool TryCreateInstance<T>(this Type type, [NotNullWhen(returnValue: true)] out T? instance) where T : class
        {
            try
            {
                var defaultCtor = type.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null,
                    Array.Empty<Type>(),
                    modifiers: null);

                instance = defaultCtor != null
                    ? (T)Activator.CreateInstance(
                        type,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        binder: null,
                        args: null,
                        culture: null)!
                    : null;

                return instance != null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create instance of {type.FullName} in {type.AssemblyQualifiedName}.", ex);
            }
        }

        /// <summary>
        /// Get the highest possible severity for any formattable document in the project.
        /// </summary>
        public static async Task<DiagnosticSeverity> GetSeverityAsync(
            this DiagnosticAnalyzer analyzer,
            Project project,
            ImmutableHashSet<string> formattablePaths,
            CancellationToken cancellationToken)
        {
            var severity = DiagnosticSeverity.Hidden;
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                return severity;
            }

            foreach (var document in project.Documents)
            {
                // Is the document formattable?
                if (document.FilePath is null || !formattablePaths.Contains(document.FilePath))
                {
                    continue;
                }

                var documentSeverity = analyzer.GetSeverity(document, project.AnalyzerOptions, compilation);
                if (documentSeverity > severity)
                {
                    severity = documentSeverity;
                }
            }

            return severity;
        }

        public static DiagnosticSeverity ToSeverity(this ReportDiagnostic reportDiagnostic)
        {
            return reportDiagnostic switch
            {
                ReportDiagnostic.Error => DiagnosticSeverity.Error,
                ReportDiagnostic.Warn => DiagnosticSeverity.Warning,
                ReportDiagnostic.Info => DiagnosticSeverity.Info,
                _ => DiagnosticSeverity.Hidden
            };
        }

        private static DiagnosticSeverity GetSeverity(
            this DiagnosticAnalyzer analyzer,
            Document document,
            AnalyzerOptions analyzerOptions,
            Compilation compilation)
        {
            var severity = DiagnosticSeverity.Hidden;

            if (!document.TryGetSyntaxTree(out var tree))
            {
                return severity;
            }

            var options = analyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(tree);

            foreach (var descriptor in analyzer.SupportedDiagnostics)
            {
                if (severity == DiagnosticSeverity.Error)
                {
                    break;
                }

                if (analyzerOptions.TryGetSeverityFromConfiguration(tree, compilation, descriptor, out var reportDiagnostic))
                {
                    var configuredSeverity = reportDiagnostic.ToSeverity();
                    if (configuredSeverity > severity)
                    {
                        severity = configuredSeverity;
                    }

                    continue;
                }

                if (TryGetSeverityFromCodeStyleOption(descriptor, compilation, options, out var codeStyleSeverity))
                {
                    if (codeStyleSeverity > severity)
                    {
                        severity = codeStyleSeverity;
                    }

                    continue;
                }
            }

            return severity;

            static bool TryGetSeverityFromCodeStyleOption(
                DiagnosticDescriptor descriptor,
                Compilation compilation,
                AnalyzerConfigOptions options,
                out DiagnosticSeverity severity)
            {
                severity = DiagnosticSeverity.Hidden;

                if (options.GetType().GetField("_backing", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.GetValue(options) is not IDictionary backingOptions)
                {
                    return false;
                }

                var parameters = new object?[] { descriptor.Id, compilation.Language, null };
                var result = (bool)(TryGetMappedOptionsMethod.Invoke(null, parameters) ?? false);

                if (!result)
                {
                    return false;
                }

                var codeStyleOptions = (IEnumerable)parameters[2]!;
                foreach (var codeStyleOptionObj in codeStyleOptions)
                {
                    if (!TryGetEditorConfigStorage(codeStyleOptionObj, out var editorConfigStorage))
                    {
                        continue;
                    }

                    var optionType = editorConfigStorage.GetType().GetGenericArguments()[0];
                    var args = new object?[] { backingOptions, optionType, null };

                    var gotOption = editorConfigStorage.GetType().GetMethod("TryGetOption")
                        ?.Invoke(editorConfigStorage, args) is true;
                    if (gotOption &&
                        TryGetOptionSeverity(args[2]!, out var codeStyleSeverity) &&
                        codeStyleSeverity > severity)
                    {
                        severity = codeStyleSeverity;
                    }
                }

                return severity != DiagnosticSeverity.Hidden;
            }

            static bool TryGetEditorConfigStorage(object optionObject, [NotNullWhen(returnValue: true)] out object? storage)
            {
                if (optionObject.GetType().GetProperty("StorageLocations")
                    ?.GetValue(optionObject) is IList storageLocations)
                {
                    foreach (var storageObj in storageLocations)
                    {
                        if (storageObj.GetType().IsAssignableTo(IEditorConfigStorageLocation2Type))
                        {
                            storage = storageObj;
                            return true;
                        }
                    }
                }

                storage = null;
                return false;
            }

            static bool TryGetOptionSeverity(object option, out DiagnosticSeverity severity)
            {
                var notificationProperty = option?.GetType().GetProperty("Notification");
                if (notificationProperty is null)
                {
                    severity = DiagnosticSeverity.Hidden;
                    return false;
                }

                var notification = notificationProperty.GetValue(option);
                if (notification?.GetType().GetProperty("Severity")
                    ?.GetValue(notification) is not ReportDiagnostic reportDiagnosticValue)
                {
                    severity = DiagnosticSeverity.Hidden;
                    return false;
                }

                severity = ToSeverity(reportDiagnosticValue);
                return true;
            }
        }
    }
}
