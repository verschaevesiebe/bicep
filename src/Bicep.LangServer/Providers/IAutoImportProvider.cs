// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Bicep.Core.Semantics.Metadata;
using Bicep.IO.Abstraction;

namespace Bicep.LanguageServer.Providers;

/// <summary>
/// Represents an exported symbol from a workspace file that can be auto-imported.
/// </summary>
/// <param name="Name">The name of the exported symbol.</param>
/// <param name="Kind">The kind of export (Type, Variable, Function).</param>
/// <param name="SourceFileUri">The URI of the file containing the export.</param>
/// <param name="Description">Optional description of the symbol for documentation.</param>
public record WorkspaceExportedSymbol(
    string Name,
    ExportMetadataKind Kind,
    IOUri SourceFileUri,
    string? Description);

/// <summary>
/// Provider for auto-import functionality that tracks exported symbols (@export decorated)
/// across workspace Bicep files, enabling IDE-style auto-import similar to Node.js module resolution.
/// </summary>
public interface IAutoImportProvider
{
    /// <summary>
    /// Gets all exported symbols from the workspace that match the given name prefix.
    /// </summary>
    /// <param name="namePrefix">Optional prefix to filter symbols by name.</param>
    /// <returns>Enumerable of matching exported symbols.</returns>
    IEnumerable<WorkspaceExportedSymbol> GetExportedSymbols(string? namePrefix = null);

    /// <summary>
    /// Gets all exported symbols from the workspace, excluding those already imported in the given file.
    /// </summary>
    /// <param name="currentFileUri">The URI of the current file to exclude already-imported symbols.</param>
    /// <param name="alreadyImportedNames">Names of symbols already imported in the current file.</param>
    /// <returns>Enumerable of exported symbols available for import.</returns>
    IEnumerable<WorkspaceExportedSymbol> GetAvailableExportsForFile(IOUri currentFileUri, ISet<string> alreadyImportedNames);

    /// <summary>
    /// Updates the provider's index for a specific file. Should be called when a file is compiled or changed.
    /// </summary>
    /// <param name="fileUri">The URI of the file to update.</param>
    /// <param name="exports">The exports from the file's semantic model.</param>
    void UpdateFileExports(IOUri fileUri, IEnumerable<ExportMetadata> exports);

    /// <summary>
    /// Removes a file from the provider's index.
    /// </summary>
    /// <param name="fileUri">The URI of the file to remove.</param>
    void RemoveFile(IOUri fileUri);

    /// <summary>
    /// Calculates the relative import path from one file to another.
    /// </summary>
    /// <param name="fromFile">The file that will contain the import statement.</param>
    /// <param name="toFile">The file being imported.</param>
    /// <returns>A relative path string suitable for use in an import statement.</returns>
    string GetRelativeImportPath(IOUri fromFile, IOUri toFile);
}
