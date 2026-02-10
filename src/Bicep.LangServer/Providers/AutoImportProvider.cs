// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Bicep.Core.Semantics.Metadata;
using Bicep.IO.Abstraction;

namespace Bicep.LanguageServer.Providers;

/// <summary>
/// Implementation of IAutoImportProvider that maintains an in-memory index
/// of all exported symbols across workspace Bicep files.
/// </summary>
public class AutoImportProvider : IAutoImportProvider
{
    // Thread-safe dictionary mapping file URIs to their exported symbols
    private readonly ConcurrentDictionary<IOUri, List<WorkspaceExportedSymbol>> _fileExports = new();

    public IEnumerable<WorkspaceExportedSymbol> GetExportedSymbols(string? namePrefix = null)
    {
        var allExports = _fileExports.Values.SelectMany(x => x);

        if (string.IsNullOrEmpty(namePrefix))
        {
            return allExports;
        }

        return allExports.Where(e => e.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<WorkspaceExportedSymbol> GetAvailableExportsForFile(IOUri currentFileUri, ISet<string> alreadyImportedNames)
    {
        return _fileExports
            .Where(kvp => !kvp.Key.Equals(currentFileUri)) // Exclude exports from the current file
            .SelectMany(kvp => kvp.Value)
            .Where(export => !alreadyImportedNames.Contains(export.Name)) // Exclude already imported
            .DistinctBy(export => (export.Name, export.SourceFileUri)); // Deduplicate
    }

    public void UpdateFileExports(IOUri fileUri, IEnumerable<ExportMetadata> exports)
    {
        var exportedSymbols = exports
            .Where(e => e.Kind != ExportMetadataKind.Error) // Skip error exports
            .Select(e => new WorkspaceExportedSymbol(
                e.Name,
                e.Kind,
                fileUri,
                e.Description))
            .ToList();

        if (exportedSymbols.Count > 0)
        {
            _fileExports[fileUri] = exportedSymbols;
        }
        else
        {
            _fileExports.TryRemove(fileUri, out _);
        }
    }

    public void RemoveFile(IOUri fileUri)
    {
        _fileExports.TryRemove(fileUri, out _);
    }

    public string GetRelativeImportPath(IOUri fromFile, IOUri toFile)
    {
        // Get file paths
        var fromPath = fromFile.TryGetFilePath();
        var toPath = toFile.TryGetFilePath();

        if (fromPath is null || toPath is null)
        {
            // Fallback to absolute path if not local files
            return toFile.ToString();
        }

        // Get directory of the source file
        var fromDirectory = Path.GetDirectoryName(fromPath) ?? "";

        // Calculate relative path
        var relativePath = Path.GetRelativePath(fromDirectory, toPath);

        // Normalize path separators to forward slashes for Bicep
        relativePath = relativePath.Replace('\\', '/');

        // Ensure path starts with ./ for relative paths in same or child directories
        if (!relativePath.StartsWith("../") && !relativePath.StartsWith("./"))
        {
            relativePath = "./" + relativePath;
        }

        return relativePath;
    }

    /// <summary>
    /// Generates the import statement text to insert for a given symbol.
    /// </summary>
    /// <param name="symbol">The symbol to import.</param>
    /// <param name="currentFileUri">The file where the import will be added.</param>
    /// <returns>The import statement text.</returns>
    public string GenerateImportStatement(WorkspaceExportedSymbol symbol, IOUri currentFileUri)
    {
        var relativePath = GetRelativeImportPath(currentFileUri, symbol.SourceFileUri);
        return $"import {{ {symbol.Name} }} from '{relativePath}'";
    }
}
