// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core;
using Bicep.Core.Semantics.Metadata;
using Bicep.IO.Abstraction;
using Bicep.LanguageServer.Providers;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bicep.LangServer.UnitTests.Providers;

[TestClass]
public class AutoImportProviderTests
{
    [TestMethod]
    public void GetExportedSymbols_WithNoFiles_ReturnsEmpty()
    {
        var provider = new AutoImportProvider();

        var symbols = provider.GetExportedSymbols();

        symbols.Should().BeEmpty();
    }

    [TestMethod]
    public void UpdateFileExports_AddsSymbolsToIndex()
    {
        var provider = new AutoImportProvider();
        var fileUri = IOUri.FromFilePath("/test/types.bicep");
        var exports = new List<ExportMetadata>
        {
            new ExportedTypeMetadata("MyType", LanguageConstants.String, "A test type"),
            new ExportedVariableMetadata("myVar", LanguageConstants.String, "A test variable", null),
        };

        provider.UpdateFileExports(fileUri, exports);

        var symbols = provider.GetExportedSymbols().ToList();
        symbols.Should().HaveCount(2);
        symbols.Should().Contain(s => s.Name == "MyType" && s.Kind == ExportMetadataKind.Type);
        symbols.Should().Contain(s => s.Name == "myVar" && s.Kind == ExportMetadataKind.Variable);
    }

    [TestMethod]
    public void GetExportedSymbols_WithNamePrefix_FiltersResults()
    {
        var provider = new AutoImportProvider();
        var fileUri = IOUri.FromFilePath("/test/types.bicep");
        var exports = new List<ExportMetadata>
        {
            new ExportedTypeMetadata("MyType", LanguageConstants.String, null),
            new ExportedTypeMetadata("OtherType", LanguageConstants.String, null),
            new ExportedTypeMetadata("MyOther", LanguageConstants.String, null),
        };

        provider.UpdateFileExports(fileUri, exports);

        var symbols = provider.GetExportedSymbols("My").ToList();
        symbols.Should().HaveCount(2);
        symbols.Should().OnlyContain(s => s.Name.StartsWith("My"));
    }

    [TestMethod]
    public void GetAvailableExportsForFile_ExcludesCurrentFile()
    {
        var provider = new AutoImportProvider();
        var file1Uri = IOUri.FromFilePath("/test/types.bicep");
        var file2Uri = IOUri.FromFilePath("/test/other.bicep");

        provider.UpdateFileExports(file1Uri, new List<ExportMetadata>
        {
            new ExportedTypeMetadata("TypeA", LanguageConstants.String, null),
        });
        provider.UpdateFileExports(file2Uri, new List<ExportMetadata>
        {
            new ExportedTypeMetadata("TypeB", LanguageConstants.String, null),
        });

        // When querying from file1, should not include file1's exports
        var available = provider.GetAvailableExportsForFile(file1Uri, new HashSet<string>()).ToList();
        available.Should().HaveCount(1);
        available.First().Name.Should().Be("TypeB");
    }

    [TestMethod]
    public void GetAvailableExportsForFile_ExcludesAlreadyImported()
    {
        var provider = new AutoImportProvider();
        var file1Uri = IOUri.FromFilePath("/test/main.bicep");
        var file2Uri = IOUri.FromFilePath("/test/types.bicep");

        provider.UpdateFileExports(file2Uri, new List<ExportMetadata>
        {
            new ExportedTypeMetadata("TypeA", LanguageConstants.String, null),
            new ExportedTypeMetadata("TypeB", LanguageConstants.String, null),
        });

        var alreadyImported = new HashSet<string> { "TypeA" };
        var available = provider.GetAvailableExportsForFile(file1Uri, alreadyImported).ToList();

        available.Should().HaveCount(1);
        available.First().Name.Should().Be("TypeB");
    }

    [TestMethod]
    public void RemoveFile_RemovesFromIndex()
    {
        var provider = new AutoImportProvider();
        var fileUri = IOUri.FromFilePath("/test/types.bicep");

        provider.UpdateFileExports(fileUri, new List<ExportMetadata>
        {
            new ExportedTypeMetadata("MyType", LanguageConstants.String, null),
        });

        provider.GetExportedSymbols().Should().HaveCount(1);

        provider.RemoveFile(fileUri);

        provider.GetExportedSymbols().Should().BeEmpty();
    }

    [TestMethod]
    public void GetRelativeImportPath_CalculatesCorrectPath()
    {
        var provider = new AutoImportProvider();

        // Same directory
        var from1 = IOUri.FromFilePath("/test/main.bicep");
        var to1 = IOUri.FromFilePath("/test/types.bicep");
        provider.GetRelativeImportPath(from1, to1).Should().Be("./types.bicep");

        // Subdirectory
        var from2 = IOUri.FromFilePath("/test/main.bicep");
        var to2 = IOUri.FromFilePath("/test/shared/types.bicep");
        provider.GetRelativeImportPath(from2, to2).Should().Be("./shared/types.bicep");

        // Parent directory
        var from3 = IOUri.FromFilePath("/test/folder/main.bicep");
        var to3 = IOUri.FromFilePath("/test/types.bicep");
        provider.GetRelativeImportPath(from3, to3).Should().Be("../types.bicep");
    }

    [TestMethod]
    public void UpdateFileExports_WithEmptyExports_RemovesFromIndex()
    {
        var provider = new AutoImportProvider();
        var fileUri = IOUri.FromFilePath("/test/types.bicep");

        // First add some exports
        provider.UpdateFileExports(fileUri, new List<ExportMetadata>
        {
            new ExportedTypeMetadata("MyType", LanguageConstants.String, null),
        });
        provider.GetExportedSymbols().Should().HaveCount(1);

        // Then update with empty exports
        provider.UpdateFileExports(fileUri, new List<ExportMetadata>());

        provider.GetExportedSymbols().Should().BeEmpty();
    }

    [TestMethod]
    public void UpdateFileExports_SkipsErrorExports()
    {
        var provider = new AutoImportProvider();
        var fileUri = IOUri.FromFilePath("/test/types.bicep");
        var exports = new List<ExportMetadata>
        {
            new ExportedTypeMetadata("ValidType", LanguageConstants.String, null),
            new DuplicatedExportMetadata("ErrorType", ["Type", "Variable"]),
        };

        provider.UpdateFileExports(fileUri, exports);

        var symbols = provider.GetExportedSymbols().ToList();
        symbols.Should().HaveCount(1);
        symbols.First().Name.Should().Be("ValidType");
    }
}
