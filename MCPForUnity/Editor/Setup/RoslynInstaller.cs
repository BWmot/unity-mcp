using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MCPForUnity.Editor.Setup
{
    public static class RoslynInstaller
    {
        private const string PluginsRelPath = "Plugins/Roslyn";

        private static readonly (string packageId, string version, string dllPath, string dllName)[] NuGetEntries =
        {
            ("microsoft.codeanalysis.common",         "4.12.0", "lib/netstandard2.0/Microsoft.CodeAnalysis.dll",                   "Microsoft.CodeAnalysis.dll"),
            ("microsoft.codeanalysis.csharp",         "4.12.0", "lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll",            "Microsoft.CodeAnalysis.CSharp.dll"),
            ("system.collections.immutable",          "8.0.0",  "lib/netstandard2.0/System.Collections.Immutable.dll",             "System.Collections.Immutable.dll"),
            ("system.reflection.metadata",            "8.0.0",  "lib/netstandard2.0/System.Reflection.Metadata.dll",               "System.Reflection.Metadata.dll"),
            // Transitive dep of Microsoft.CodeAnalysis.* on netstandard2.0. Without it, Roslyn's StringTable
            // static cctor throws FileNotFoundException for v6.0.0.0 and every Roslyn entry point fails to
            // initialize. Unity ships a v4.x of this assembly which does NOT satisfy the v6 reference.
            ("system.runtime.compilerservices.unsafe","6.0.0",  "lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll",   "System.Runtime.CompilerServices.Unsafe.dll"),
        };

        public static bool IsInstalled()
        {
            string folder = Path.Combine(Application.dataPath, PluginsRelPath);
            foreach (var entry in NuGetEntries)
            {
                string path = Path.Combine(folder, entry.dllName);
                if (!File.Exists(path))
                    return false;

                // Defense-in-depth: a stale DLL whose assembly version is older than what
                // Roslyn references (e.g. a v4.x System.Runtime.CompilerServices.Unsafe
                // shadowing the v6 we actually need) would still satisfy file-existence but
                // leave Roslyn unable to load. Compare the on-disk assembly version against
                // each entry's declared NuGet version, treating "older or unreadable" as not
                // installed so Install() can rewrite it.
                if (Version.TryParse(entry.version, out var requiredVersion))
                {
                    try
                    {
                        var actual = AssemblyName.GetAssemblyName(path).Version;
                        if (actual == null || actual < requiredVersion)
                            return false;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static void Install(bool interactive = true)
        {
            if (IsInstalled() && interactive)
            {
                if (!EditorUtility.DisplayDialog(
                        "Roslyn Already Installed",
                        $"Roslyn DLLs are already present in Assets/{PluginsRelPath}.\nReinstall?",
                        "Reinstall", "Cancel"))
                    return;
            }

            string destFolder = Path.Combine(Application.dataPath, PluginsRelPath);

            try
            {
                Directory.CreateDirectory(destFolder);

                for (int i = 0; i < NuGetEntries.Length; i++)
                {
                    var (packageId, pkgVersion, dllPathInZip, dllName) = NuGetEntries[i];

                    if (interactive)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Installing Roslyn",
                            $"Downloading {packageId} v{pkgVersion}...",
                            (float)i / NuGetEntries.Length);
                    }

                    string url =
                        $"https://api.nuget.org/v3-flatcontainer/{packageId}/{pkgVersion}/{packageId}.{pkgVersion}.nupkg";

                    using (var request = UnityWebRequest.Get(url))
                    {
                        request.timeout = 30;
                        request.SendWebRequest();
                        while (!request.isDone)
                            System.Threading.Thread.Sleep(50);

                        if (request.result != UnityWebRequest.Result.Success)
                            throw new Exception($"Failed to download {packageId}: {request.error}");

                        byte[] nupkgBytes = request.downloadHandler.data;
                        byte[] dllBytes = ExtractFileFromZip(nupkgBytes, dllPathInZip);

                        if (dllBytes == null)
                        {
                            Debug.LogError($"[MCP] Could not find {dllPathInZip} in {packageId}.{pkgVersion}.nupkg");
                            continue;
                        }

                        string destPath = Path.Combine(destFolder, dllName);
                        File.WriteAllBytes(destPath, dllBytes);
                        Debug.Log($"[MCP] Extracted {dllName} ({dllBytes.Length / 1024}KB) → Assets/{PluginsRelPath}/{dllName}");
                    }
                }

                if (interactive)
                    EditorUtility.DisplayProgressBar("Installing Roslyn", "Refreshing assets...", 0.95f);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (interactive)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog(
                        "Roslyn Installed",
                        $"Roslyn DLLs and dependencies installed to Assets/{PluginsRelPath}/.\n\n" +
                        "The runtime_compilation tool is now available via MCP.",
                        "OK");
                }

                Debug.Log($"[MCP] Roslyn installation complete ({NuGetEntries.Length} DLLs). runtime_compilation is now available.");
            }
            catch (Exception e)
            {
                if (interactive) EditorUtility.ClearProgressBar();
                Debug.LogError($"[MCP] Failed to install Roslyn: {e}");

                if (interactive)
                {
                    EditorUtility.DisplayDialog(
                        "Installation Failed",
                        $"Could not download Roslyn DLLs:\n{e.Message}\n\n" +
                        "You can manually download Microsoft.CodeAnalysis.CSharp from NuGet " +
                        "and place the DLLs in Assets/Plugins/Roslyn/.",
                        "OK");
                }
            }
        }

        private static byte[] ExtractFileFromZip(byte[] zipBytes, string entryPath)
        {
            entryPath = entryPath.Replace('\\', '/');

#if UNITY_2021_2_OR_NEWER
            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.Replace('\\', '/').Equals(entryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var entryStream = entry.Open())
                        using (var output = new MemoryStream())
                        {
                            entryStream.CopyTo(output);
                            return output.ToArray();
                        }
                    }
                }
            }
#else
            // Minimal ZIP reader for Unity < 2021.2 (no System.IO.Compression.ZipArchive)
            using (var ms = new MemoryStream(zipBytes))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                // Find End of Central Directory Record (signature: 0x06054b50)
                long eocdPos = -1;
                for (long i = ms.Length - 22; i >= 0; i--)
                {
                    ms.Position = i;
                    if (reader.ReadUInt32() == 0x06054b50)
                    {
                        eocdPos = i;
                        break;
                    }
                }
                if (eocdPos < 0) return null;

                ms.Position = eocdPos + 16; // skip to central directory offset
                uint cdSize = reader.ReadUInt32();
                uint cdOffset = reader.ReadUInt32();

                // Parse central directory to find entry
                ms.Position = cdOffset;
                long cdEnd = cdOffset + cdSize;
                uint targetLocalOffset = 0;
                ushort targetMethod = 0;
                uint targetCompressedSize = 0;

                while (ms.Position < cdEnd)
                {
                    if (reader.ReadUInt32() != 0x02014b50) break;
                    ms.Position += 6; // skip version, flags
                    ushort method = reader.ReadUInt16();
                    ms.Position += 12; // skip time, date, crc, sizes
                    uint compSize = reader.ReadUInt32();
                    uint uncompSize = reader.ReadUInt32();
                    ushort nameLen = reader.ReadUInt16();
                    ushort extraLen = reader.ReadUInt16();
                    ushort commentLen = reader.ReadUInt16();
                    ms.Position += 8; // skip disk, attrs
                    uint localOffset = reader.ReadUInt32();
                    byte[] nameBytes = reader.ReadBytes(nameLen);
                    ms.Position += extraLen + commentLen;

                    string name = System.Text.Encoding.UTF8.GetString(nameBytes).Replace('\\', '/');
                    if (name.Equals(entryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        targetLocalOffset = localOffset;
                        targetMethod = method;
                        targetCompressedSize = compSize;
                        break;
                    }
                }

                if (targetCompressedSize == 0 && targetMethod == 0) return null;

                // Read local file header
                ms.Position = targetLocalOffset;
                if (reader.ReadUInt32() != 0x04034b50) return null;
                ms.Position += 4; // skip version, flags
                ushort localMethod = reader.ReadUInt16();
                ms.Position += 12; // skip time, date, crc, sizes
                uint localCompSize = reader.ReadUInt32();
                uint localUncompSize = reader.ReadUInt32();
                ushort localNameLen = reader.ReadUInt16();
                ushort localExtraLen = reader.ReadUInt16();
                ms.Position += localNameLen + localExtraLen;

                byte[] data = reader.ReadBytes((int)localCompSize);

                if (localMethod == 0) // stored
                    return data;
                else if (localMethod == 8) // deflate
                {
                    using (var deflateStream = new System.IO.Compression.DeflateStream(
                        new MemoryStream(data), System.IO.Compression.CompressionMode.Decompress))
                    using (var output = new MemoryStream())
                    {
                        deflateStream.CopyTo(output);
                        return output.ToArray();
                    }
                }

                return null;
            }
#endif

            return null;
        }
    }
}
