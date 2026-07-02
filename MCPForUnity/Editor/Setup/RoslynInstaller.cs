using System;
using System.IO;
using System.IO.Compression;
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
            ("microsoft.codeanalysis.common",         "4.8.0",  "lib/netstandard2.0/Microsoft.CodeAnalysis.dll",                   "Microsoft.CodeAnalysis.dll"),
            ("microsoft.codeanalysis.csharp",         "4.8.0",  "lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll",            "Microsoft.CodeAnalysis.CSharp.dll"),
            ("system.collections.immutable",          "7.0.0",  "lib/netstandard2.0/System.Collections.Immutable.dll",             "System.Collections.Immutable.dll"),
            ("system.reflection.metadata",            "7.0.0",  "lib/netstandard2.0/System.Reflection.Metadata.dll",               "System.Reflection.Metadata.dll"),
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

        public static void Uninstall()
        {
            string folder = Path.Combine(Application.dataPath, PluginsRelPath);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }

            AssetDatabase.Refresh();
            EditorApplication.delayCall += () =>
            {
                var window = EditorWindow.GetWindow<MCPForUnity.Editor.Windows.MCPForUnityEditorWindow>(false, "MCP For Unity", true);
                if (window != null)
                    window.Repaint();
            };
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
            // Minimal ZIP reader for Unity < 2021.2
            // Scan local file headers sequentially and extract the target entry.
            using (var ms = new MemoryStream(zipBytes))
            using (var reader = new BinaryReader(ms))
            {
                while (ms.Position + 30 <= ms.Length)
                {
                    uint sig = reader.ReadUInt32();
                    if (sig != 0x04034b50)
                        break;

                    ushort version = reader.ReadUInt16();
                    ushort flags = reader.ReadUInt16();
                    ushort method = reader.ReadUInt16();
                    ushort modTime = reader.ReadUInt16();
                    ushort modDate = reader.ReadUInt16();
                    uint crc32 = reader.ReadUInt32();
                    uint compSize = reader.ReadUInt32();
                    uint uncompSize = reader.ReadUInt32();
                    ushort nameLen = reader.ReadUInt16();
                    ushort extraLen = reader.ReadUInt16();

                    if (nameLen == 0 || ms.Position + nameLen + extraLen > ms.Length)
                        return null;

                    byte[] nameBytes = reader.ReadBytes(nameLen);
                    ms.Position += extraLen;

                    string name = System.Text.Encoding.UTF8.GetString(nameBytes).Replace('\\', '/');
                    string safeName = name.TrimStart('/');

                    bool isTarget = safeName.Equals(entryPath, StringComparison.OrdinalIgnoreCase);
                    if (!isTarget)
                    {
                        if (compSize > 0)
                        {
                            if (ms.Position + compSize > ms.Length)
                                return null;
                            ms.Position += compSize;
                        }
                        else
                        {
                            // ZIP entry with data descriptor or unknown size is not supported here
                            return null;
                        }
                        continue;
                    }

                    if (compSize == 0 || ms.Position + compSize > ms.Length)
                        return null;

                    byte[] data = reader.ReadBytes((int)compSize);

                    if (method == 0) // stored
                        return data;

                    if (method == 8) // deflate
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
            }
#endif

            return null;
        }
    }
}
