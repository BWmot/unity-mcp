using System;
using System.IO;

namespace MCPForUnity.Editor.Dependencies
{
    public static class CodeExecutorDependency
    {
        public static bool IsInstalled()
        {
            return !string.IsNullOrEmpty(FindPackageManifest())
                   || !string.IsNullOrEmpty(FindProjectAssetPath())
                   || !string.IsNullOrEmpty(FindExecutionHelperType());
        }

        public static string GetInstallHint()
        {
            return "Install the com.chenpipi.code-executor package, or place the package under Assets/Scripts/Editor/ChenPipi/unity-code-executor.";
        }

        public static string GetDetectedSource()
        {
            var packagePath = FindPackageManifest();
            if (!string.IsNullOrEmpty(packagePath))
            {
                return packagePath;
            }

            var assetPath = FindProjectAssetPath();
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            var typeName = FindExecutionHelperType();
            return typeName ?? string.Empty;
        }

        private static string FindPackageManifest()
        {
            var paths = new[]
            {
                Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "com.chenpipi.code-executor", "package.json"),
                Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Editor", "ChenPipi", "unity-code-executor", "package.json")
            };

            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static string FindProjectAssetPath()
        {
            var paths = new[]
            {
                Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Editor", "ChenPipi", "unity-code-executor"),
                Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Editor", "ChenPipi", "unity-code-executor-custom-injector")
            };

            foreach (var path in paths)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        return Path.GetFullPath(path);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static string FindExecutionHelperType()
        {
            return Type.GetType("ChenPipi.CodeExecutor.Examples.ExecutionHelperXLua, Assembly-CSharp")?.FullName
                   ?? Type.GetType("ChenPipi.CodeExecutor.Examples.ExecutionHelperXLua")?.FullName;
        }
    }
}
