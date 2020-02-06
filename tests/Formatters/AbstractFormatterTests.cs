// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.ExternalAccess.Format;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.CodingConventions;
using Microsoft.VisualStudio.Composition;
using Xunit;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public abstract class AbstractFormatterTest
    {
        private static readonly Lazy<IExportProviderFactory> ExportProviderFactory;

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

        protected virtual string DefaultFilePathPrefix { get; } = "Test";

        protected virtual string DefaultTestProjectName { get; } = "TestProject";

        protected virtual string DefaultTestProjectPath => "." + Path.DirectorySeparatorChar + DefaultTestProjectName + "." + DefaultFileExt + "proj";

        protected virtual string DefaultFilePath => DefaultFilePathPrefix + 0 + "." + DefaultFileExt;

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

        private protected Task<SourceText> TestAsync(string testCode, string expectedCode, IReadOnlyDictionary<string, string> editorConfig)
        {
            return TestAsync(testCode, expectedCode, editorConfig, Encoding.UTF8);
        }

        private protected async Task<SourceText> TestAsync(string testCode, string expectedCode, IReadOnlyDictionary<string, string> editorConfig, Encoding encoding)
        {
            var text = SourceText.From(testCode, encoding);
            TestState.Sources.Add(text);

            var analyzerConfig = CreateEditorConfigText(editorConfig);

            var solution = GetSolution(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray(), analyzerConfig);
            var project = solution.Projects.Single();
            var document = project.Documents.Single();
            var formatOptions = new FormatOptions(
                workspaceFilePath: project.FilePath,
                workspaceType: WorkspaceType.Folder,
                logLevel: LogLevel.Trace,
                formatType: FormatType.Whitespace,
                saveFormattedFiles: false,
                changesAreErrors: false,
                filesToFormat: ImmutableHashSet.Create(document.FilePath));

            var filesToFormat = await GetOnlyFileToFormatAsync(solution, editorConfig);

            var formattedSolution = await Formatter.FormatAsync(solution, filesToFormat, formatOptions, Logger, default);
            var formattedDocument = GetOnlyDocument(formattedSolution);
            var formattedText = await formattedDocument.GetTextAsync();

            Assert.Equal(expectedCode, formattedText.ToString());

            return formattedText;
        }

        private string CreateEditorConfigText(IReadOnlyDictionary<string, string> editorConfig)
        {
            var entries = string.Join(Environment.NewLine, editorConfig.Select(kvp => $"{kvp.Key} = {kvp.Value}"));
            return $@"
root = true

[*.{DefaultFileExt}]

{entries}
";
        }

        /// <summary>
        /// Gets the only <see cref="Document"/> along with related options and conventions.
        /// </summary>
        /// <param name="solution">A Solution containing a single Project containing a single Document.</param>
        /// <returns>The document contained within along with option set and coding conventions.</returns>
        protected async Task<ImmutableArray<(DocumentId, OptionSet, ICodingConventionsSnapshot)>> GetOnlyFileToFormatAsync(Solution solution, IReadOnlyDictionary<string, string> editorConfig)
        {
            var document = GetOnlyDocument(solution);
            var options = (OptionSet)await document.GetOptionsAsync();
            ICodingConventionsSnapshot codingConventions = new TestCodingConventionsSnapshot(editorConfig);

            return ImmutableArray.Create((document.Id, options, codingConventions));
        }

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
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="analyzerConfig">EditorConfig source that should be added to the project.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private Solution GetSolution((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string analyzerConfig)
        {
            var project = CreateProject(sources, additionalFiles, additionalMetadataReferences, analyzerConfig, Language);
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
        /// <param name="analyzerConfig">EditorConfig source that should be added to the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected Project CreateProject((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string analyzerConfig, string language)
        {
            language = language ?? language;
            return CreateProjectImpl(sources, additionalFiles, additionalMetadataReferences, analyzerConfig, language);
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="analyzerConfig">EditorConfig source that should be added to the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected virtual Project CreateProjectImpl((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string analyzerConfig, string language)
        {
            var projectId = ProjectId.CreateNewId(debugName: DefaultTestProjectName);
            var workspace = CreateSolution(projectId, language).Workspace;
            var solution = workspace.CurrentSolution;

            var tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            solution = solution.AddMetadataReferences(projectId, additionalMetadataReferences);

            for (var i = 0; i < sources.Length; i++)
            {
                (var newFileName, var source) = sources[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, source, filePath: Path.Combine(tempDirectory, newFileName));
            }

            for (var i = 0; i < additionalFiles.Length; i++)
            {
                (var newFileName, var source) = additionalFiles[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddAdditionalDocument(documentId, newFileName, source);
            }

            // Add editorconfig to the analyzer config documents and register document options provider so
            // that the compiler will process it.
            {
                var documentId = DocumentId.CreateNewId(projectId, debugName: ".editorconfig");
                solution = solution.AddAnalyzerConfigDocument(documentId, ".editorconfig", SourceText.From(analyzerConfig), filePath: Path.Combine(tempDirectory, ".editorconfig"));

                workspace.TryApplyChanges(solution);
                CodeStyleAnalyzers.RegisterDocumentOptionsProvider(workspace);
            }

            return workspace.CurrentSolution.GetProject(projectId);
        }

        /// <summary>
        /// Creates a solution that will be used as parent for the sources that need to be checked.
        /// </summary>
        /// <param name="projectId">The project identifier to use.</param>
        /// <param name="language">The language for which the solution is being created.</param>
        /// <returns>The created solution.</returns>
        protected virtual Solution CreateSolution(ProjectId projectId, string language)
        {
            var compilationOptions = CreateCompilationOptions();

            var xmlReferenceResolver = new TestXmlReferenceResolver();
            foreach (var xmlReference in XmlReferences)
            {
                xmlReferenceResolver.XmlReferences.Add(xmlReference.Key, xmlReference.Value);
            }

            compilationOptions = compilationOptions.WithXmlReferenceResolver(xmlReferenceResolver);

            var workspace = CreateWorkspace();
            var solution = workspace
                .CurrentSolution
                .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), DefaultTestProjectName, DefaultTestProjectName, language, filePath: DefaultTestProjectPath))
                .WithProjectCompilationOptions(projectId, compilationOptions)
                .AddMetadataReference(projectId, MetadataReferences.CorlibReference)
                .AddMetadataReference(projectId, MetadataReferences.SystemReference)
                .AddMetadataReference(projectId, MetadataReferences.SystemCoreReference)
                .AddMetadataReference(projectId, MetadataReferences.CodeAnalysisReference)
                .AddMetadataReference(projectId, MetadataReferences.SystemCollectionsImmutableReference);

            if (language == LanguageNames.VisualBasic)
            {
                solution = solution.AddMetadataReference(projectId, MetadataReferences.MicrosoftVisualBasicReference);
            }

            var parseOptions = solution.GetProject(projectId).ParseOptions;
            solution = solution.WithProjectParseOptions(projectId, parseOptions.WithDocumentationMode(DocumentationMode.Diagnose));

            workspace.TryApplyChanges(solution);

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
