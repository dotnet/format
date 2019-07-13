﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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
        /// <value>
        /// The language name used for the test.
        /// </value>
        public abstract string Language { get; }

        private string TestCode
        {
            set
            {
                if (value != null)
                {
                    TestState.Sources.Add(value);
                }
            }
        }

        private static ILogger Logger => new TestLogger();
        private static EditorConfigOptionsApplier OptionsApplier => new EditorConfigOptionsApplier();

        public SolutionState TestState { get; }

        private protected async Task TestAsync(string testCode, string expectedCode, IReadOnlyDictionary<string, string> editorConfig)
        {
            TestCode = testCode;

            var solution = GetSolution(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray());
            var project = solution.Projects.Single();
            var document = project.Documents.Single();
            var options = (OptionSet)await document.GetOptionsAsync();
            var formatOptions = new FormatOptions(
                workspaceFilePath: project.FilePath,
                isSolution: false,
                logLevel: LogLevel.Trace,
                saveFormattedFiles: false,
                changesAreErrors: false,
                filesToFormat: ImmutableHashSet.Create<string>(document.FilePath));

            ICodingConventionsSnapshot codingConventions = new TestCodingConventionsSnapshot(editorConfig);
            options = OptionsApplier.ApplyConventions(options, codingConventions, Language);

            var filesToFormat = new[] { (document, options, codingConventions) }.ToImmutableArray();

            var formattedSolution = await Formatter.FormatAsync(solution, filesToFormat, formatOptions, Logger, default);
            var formattedDocument = formattedSolution.Projects.Single().Documents.Single();
            var formattedText = await formattedDocument.GetTextAsync();

            Assert.Equal(expectedCode, formattedText.ToString());
        }

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

        public Document GetTestDocument(string testCode)
        {
            TestCode = testCode;
            var solution = GetSolution(TestState.Sources.ToArray(), TestState.AdditionalFiles.ToArray(), TestState.AdditionalReferences.ToArray());
            return solution.Projects.Single().Documents.Single();
        }

        /// <summary>
        /// Given an array of strings as sources and a language, turn them into a <see cref="Project"/> and return the
        /// solution.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <returns>A solution containing a project with the specified sources and additional files.</returns>
        private Solution GetSolution((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences)
        {
            var project = CreateProject(sources, additionalFiles, additionalMetadataReferences, Language);
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
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected Project CreateProject((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string language)
        {
            language = language ?? language;
            return CreateProjectImpl(sources, additionalFiles, additionalMetadataReferences, language);
        }

        /// <summary>
        /// Create a project using the input strings as sources.
        /// </summary>
        /// <param name="sources">Classes in the form of strings.</param>
        /// <param name="additionalFiles">Additional documents to include in the project.</param>
        /// <param name="additionalMetadataReferences">Additional metadata references to include in the project.</param>
        /// <param name="language">The language the source classes are in. Values may be taken from the
        /// <see cref="LanguageNames"/> class.</param>
        /// <returns>A <see cref="Project"/> created out of the <see cref="Document"/>s created from the source
        /// strings.</returns>
        protected virtual Project CreateProjectImpl((string filename, SourceText content)[] sources, (string filename, SourceText content)[] additionalFiles, MetadataReference[] additionalMetadataReferences, string language)
        {
            var projectId = ProjectId.CreateNewId(debugName: DefaultTestProjectName);
            var solution = CreateSolution(projectId, language);

            solution = solution.AddMetadataReferences(projectId, additionalMetadataReferences);

            for (var i = 0; i < sources.Length; i++)
            {
                (var newFileName, var source) = sources[i];
                var documentId = DocumentId.CreateNewId(projectId, debugName: newFileName);
                solution = solution.AddDocument(documentId, newFileName, source, filePath: newFileName);
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

            var solution = CreateWorkspace()
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

            foreach (var transform in OptionsTransforms)
            {
                solution.Workspace.Options = transform(solution.Workspace.Options);
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
