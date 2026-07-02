using UnityEditor;

namespace MCPForUnity.Editor.Tools.Lua
{
    /// <summary>
    /// Keeps the Lua tool assembly participating in Unity editor reload / startup flows.
    /// Tool discovery itself is attribute-based; this hook only ensures the type is loaded.
    /// </summary>
    public static class LuaToolsRegistration
    {
        private const string MigrationKey = "MCPForUnity.LuaTools.AutoRegisterMigration_v1";
        private const string ToolEnabledPrefix = "MCPForUnity.ToolEnabled.";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            // One-time migration: clear old EditorPrefs values that were written
            // when AutoRegister was false. After clearing, ToolDiscoveryService
            // will re-initialize them with the new AutoRegister=true default.
            if (!EditorPrefs.GetBool(MigrationKey, false))
            {
                EditorPrefs.DeleteKey(ToolEnabledPrefix + "lua_execute");
                EditorPrefs.DeleteKey(ToolEnabledPrefix + "lua_backend_status");
                EditorPrefs.SetBool(MigrationKey, true);
                UnityEngine.Debug.Log("[MCPForUnity] Lua tools migration: cleared old EditorPrefs, AutoRegister=true will take effect.");
            }
        }
    }
}