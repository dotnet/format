﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Microsoft.CodeAnalysis.Tools.Tests.Utilities;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Composition;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public abstract class AbstractFormatterTest
    {
        private static MetadataReference CorlibReference => MetadataReference.CreateFromFile(typeof(object).Assembly.Location).WithAliases(ImmutableArray.Create("global", "corlib"));
        private static MetadataReference SystemReference => MetadataReference.CreateFromFile(typeof(System.Diagnostics.Debug).Assembly.Location).WithAliases(ImmutableArray.Create("global", "system"));
        private static MetadataReference SystemCoreReference => MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static MetadataReference CodeAnalysisReference => MetadataReference.CreateFromFile(typeof(Compilation).Assembly.Location);

        private static MetadataReference SystemCollectionsImmutableReference => MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location);
        private static MetadataReference MicrosoftVisualBasicReference => MetadataReference.CreateFromFile(typeof(Microsoft.VisualBasic.Strings).Assembly.Location);

        private static Lazy<IExportProviderFactory> ExportProviderFactory { get; }

        static AbstractFormatterTest()
        {
            ExportProviderFactory = new Lazy<IExportProviderFactory>(
                () =>
                {
                    var discovery = new AttributedPartDiscovery(Resolver.DefaultInstance, isNonPublicSupported: true);
                    var parts = Task.Run(() => discovery.CreatePartsAsync(MefHostServices.DefaultAssemblies)).GetAwaiter().GetResult();
                    var catalog = ComposableCatalog.Create(Resolver.DefaultInstance).AddParts(parts);

                    var configuration = CompositionConfiguration.Create(catalog);
                    var runtimeComposition = RuntimeComposition.CreateRuntimeComposition(configuration);
                    return runtimeComposition.CreateExportProviderFactory();
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        protected virtual string DefaultFilePathPrefix => "Test";

        protected virtual string DefaultTestProjectName => "TestProject";

        // This folder path needs to appear rooted when adding the AnalyzerConfigDocument.
        // We achieve this by prepending a directory separator.
        protected virtual string DefaultFolderPath => Path.DirectorySeparatorChar + DefaultTestProjectName;

        protected virtual string DefaultTestProjectPath => Path.Combine(DefaultFolderPath, $"{DefaultTestProjectName}.{DefaultFileExt}proj");

        protected virtual string DefaultEditorConfigPath => Path.Combine(DefaultFolderPath + ".editorconfig");

        protected virtual string DefaultFilePath => Path.Combine(DefaultFolderPath, $"{DefaultFilePathPrefix}0.{DefaultFileExt}");

        protected abstract string DefaultFileExt { get; }

        private protected abstract ICodeFormatter Formatter { get; }

        protected AbstractFormatterTest()
        {
            TestState = new SolutionState(DefaultFilePathPrefix, DefaultFileExt);
        }

        /// <summary>
        /// Gets the language name used for the test.
        /// </summary>
        public abstract string Language { get; }

        private static ILogger Logger => new TestLogger();

        public SolutionState TestState { get; }

        private protected string ToEditorConfig(IReadOnlyDictionary<string, string> editorConfig) => $@"root = true

[*.{DefaultFileExt}]
{ string.Join(Environment.NewLine, editorConfig.Select(kvp => $"{kvp.Key} = {kvp.Value}")) }
";

        private protected Task<SourceText> AssertCodeUnchangedAsync(
            string code,
            IReadOnlyDictionary<string, string> editorConfig,
            Encoding encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error)
        {
            return AssertCodeChangedAsync(code, code, ToEditorConfig(editorConfig), encoding, fixCategory, codeStyleSeverity, analyzerSeverity);
        }

        private protected Task<SourceText> AssertCodeChangedAsync(
            string testCode,
            string expectedCode,
            IReadOnlyDictionary<string, string> editorConfig,
            Encoding encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error)
        {
            return AssertCodeChangedAsync(testCode, expectedCode, ToEditorConfig(editorConfig), encoding, fixCategory, codeStyleSeverity, analyzerSeverity);
        }

        private protected async Task<SourceText> AssertCodeChangedAsync(
            string testCode,
            string expectedCode,
            string editorConfig,
            Encoding encoding = null,
            FixCategory fixCategory = FixCategory.Whitespace,
            DiagnosticSeverity codeStyleSeverity = DiagnosticSeverity.Error,
            DiagnosticSeverity analyzerSeverity = DiagnosticSeverity.Error)
        {
            var text = SourceText.From(testCode, encoding ?? Encoding.UTF8);
            TestState.Sources.Add(text);

            var solution = GetSolution(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray(), editorConfig);
            var project = solution.Projects.Single();
            var document = project.Documents.Single();

            var fileMatcher = SourceFileMatcher.CreateMatcher(new[] { document.FilePath }, exclude: Array.Empty<string>());
            var formatOptions = new FormatOptions(
                WorkspaceFilePath: project.FilePath,
                WorkspaceType: WorkspaceType.Folder,
                LogLevel: LogLevel.Trace,
                fixCategory,
                codeStyleSeverity,
                analyzerSeverity,
                SaveFormattedFiles: true,
                ChangesAreErrors: false,
                fileMatcher,
                ReportPath: string.Empty,
                IncludeGeneratedFiles: false);

            var pathsToFormat = GetOnlyFileToFormat(solution);

            var formattedSolution = await Formatter.FormatAsync(solution, pathsToFormat, formatOptions, Logger, new List<FormattedFile>(), default);
            var formattedDocument = GetOnlyDocument(formattedSolution);
            var formattedText = await formattedDocument.GetTextAsync();

            Assert.Equal(expectedCode, formattedText.ToString());

            return formattedText;
        }

        /// <summary>
        /// Gets the only <see cref="DocumentId"/>.
        /// </summary>
        /// <param name="solution">A Solution containing a single Project containing a single Document.</param>
        /// <returns>The only document id.</returns>
        internal ImmutableArray<DocumentId> GetOnlyFileToFormat(Solution solution) => ImmutableArray.Create(GetOnlyDocument(solution).Id);

        /// <summary>
        /// Gets the only <see cref="Document"/> contained within the only <see cref="Project"/> within the <see cref="Solution"/>.
        /// </summary>
        /// <param name="solution">A Solution containing a single Project containing a single Document.</param>
        /// <returns>The document contained within.</returns>
        public Document GetOnlyDocument(Solution solution) => solution.Projects.Single().Documents.Single();

        /// <summary>
        /// Gets the collection of inputs to provide to the XML documentation resolver.
        /// </summary>
        /// <remarks>
        /// <para>Files in this collection may be referenced via <c>&lt;include&gt;</c> elements in documentation
        /// comments.</para>
        /// </remarks>
        public Dictionary<string, string> XmlReferences { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets a collection of transformation functions to apply to <see cref="Workspace.Options"/> during diagnostic
        /// or code fix test setup.
        /// </summary>
        public List<Func<OptionSet, OptionSet>> OptionsTransforms { get; } = new List<Func<OptionSet, OptionSet>>();

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="editorConfig">The .editorconfig to apply to this solution.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private protected Solution GetSolution((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, IReadOnlyDictionary<string, string> editorConfig)
        {
            return GetSolution(sources, additionalFiles, additionalMetadataReferences, ToEditorConfig(editorConfig));
        }

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="editorConfig">The .editorconfig to apply to this solution.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private protected Solution GetSolution((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string editorConfig)
        {
            var project = CreateProject(sources, additionalFiles, additionalMetadataReferences, Language, SourceText.From(editorConfig, Encoding.UTF8));
            return project.Solution;
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <remarks>
        /// <para>This method first creates a <see cref="Project"/> by calling <see cref="CreateProjectImpl"/>, and then
        /// applies compilation options to the project by calling <see cref="ApplyCompilationOptions"/>.</para>
        /// </remarks>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this project.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected Project CreateProject((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string language, SourceText editorConfigText)
        {
            language ??= Language;
            return CreateProjectImpl(sources, additionalFiles, additionalMetadataReferences, language, editorConfigText);
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this project.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected virtual Project CreateProjectImpl((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string language, SourceText editorConfigText)
        {
            var projectId = ProjectId.CreateNewId(debugName: DefaultTestProjectName);
            var solution = CreateSolution(projectId, language, editorConfigText);

            solution = solution.AddMetadataReferences(projectId, additionalMetadataReferences);

            for (var i = 0; i < sources.Length; i++)
            {
                (var newFileName, var source) = sources[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, source, filePath: Path.Combine(DefaultTestProjectPath, newFileName));
            }

            for (var i = 0; i < additionalFiles.Length; i++)
            {
                (var newFileName, var source) = additionalFiles[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddAdditionalDocument(documentId, newFileName, source);
            }

            return solution.GetProject(projectId);
        }

        /// <summary>
        /// Creates a solution that will be used as parent for the sources that need to be checked.
        /// </summary>
        /// <param name="projectId">The project identifier to use.</param>
        /// <param name="language">The language for which the solution is being created.</param>
        /// <param name="editorConfigText">The .editorconfig to apply to this solution.</param>
        /// <returns>The created solution.</returns>
        protected virtual Solution CreateSolution(ProjectId projectId, string language, SourceText editorConfigText)
        {
            var compilationOptions = CreateCompilationOptions();

            var xmlReferenceResolver = new TestXmlReferenceResolver();
            foreach (var xmlReference in XmlReferences)
            {
                xmlReferenceResolver.XmlReferences.Add(xmlReference.Key, xmlReference.Value);
            }

            compilationOptions = compilationOptions.WithXmlReferenceResolver(xmlReferenceResolver);

            var editorConfigDocument = DocumentInfo.Create(
                DocumentId.CreateNewId(projectId, DefaultEditorConfigPath),
                name: DefaultEditorConfigPath,
                loader: TextLoader.From(TextAndVersion.Create(editorConfigText, VersionStamp.Create())),
                filePath: DefaultEditorConfigPath);

            var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), DefaultTestProjectName, DefaultTestProjectName, language, filePath: DefaultTestProjectPath)
                .WithAnalyzerConfigDocuments(ImmutableArray.Create(editorConfigDocument));

            var solution = CreateWorkspace()
                .CurrentSolution
                .AddProject(projectInfo)
                .WithProjectCompilationOptions(projectId, compilationOptions)
                .AddMetadataReference(projectId, CorlibReference)
                .AddMetadataReference(projectId, SystemReference)
                .AddMetadataReference(projectId, SystemCoreReference)
                .AddMetadataReference(projectId, CodeAnalysisReference)
                .AddMetadataReference(projectId, SystemCollectionsImmutableReference);

            if (language == LanguageNames.VisualBasic)
            {
                solution = solution.AddMetadataReference(projectId, MicrosoftVisualBasicReference);
            }

            foreach (var transform in OptionsTransforms)
            {
                solution.Workspace.TryApplyChanges(solution.WithOptions(transform(solution.Workspace.Options)));
            }

            var parseOptions = solution.GetProject(projectId).ParseOptions;
            solution = solution.WithProjectParseOptions(projectId, parseOptions.WithDocumentationMode(DocumentationMode.Diagnose));

            return solution;
        }

        public virtual AdhocWorkspace CreateWorkspace()
        {
            var exportProvider = ExportProviderFactory.Value.CreateExportProvider();
            var host = MefHostServices.Create(exportProvider.AsCompositionContext());
            return new AdhocWorkspace(host);
        }

        protected abstract CompilationOptions CreateCompilationOptions();
    }
}
