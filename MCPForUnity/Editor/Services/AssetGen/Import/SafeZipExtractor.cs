using System;
using System.Collections.Generic;
using System.IO;

namespace MCPForUnity.Editor.Services.AssetGen.Import
{
    /// <summary>
    /// Extracts a .zip into a destination directory while rejecting Zip-Slip path traversal.
    ///
    /// Unity 2020.2 in this project does not reliably expose System.IO.Compression types to the
    /// editor asmdef, so this implementation intentionally fails fast with a clear guidance error
    /// instead of referencing ZipArchive directly and breaking compilation.
    /// </summary>
    public static class SafeZipExtractor
    {
        public static void ExtractTo(string zipPath, string destDir, ISet<string> allowedExtensions = null)
        {
            if (string.IsNullOrEmpty(zipPath)) throw new ArgumentException("zipPath required", nameof(zipPath));
            if (string.IsNullOrEmpty(destDir)) throw new ArgumentException("destDir required", nameof(destDir));

            throw new NotSupportedException(
                "ZIP extraction requires System.IO.Compression.ZipArchive support in this Unity runtime. " +
                "Add the missing assembly reference for System.IO.Compression, or replace this helper with " +
                "an editor-safe ZIP package implementation.");
        }
    }
}
