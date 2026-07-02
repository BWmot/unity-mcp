namespace MCPForUnity.Editor.Tools.Lua
{
    public static class LuaMcpSettings
    {
        public static bool EnableLuaTool = true;
        public static bool EnableCodeExecutorIntegration = true;
        public static LuaExecuteMode PreferredBackend = LuaExecuteMode.CodeExecutorFirst;
        public static bool AllowFallbackToXLua = true;
        public static bool AllowFallbackToNLua = false;
    }
}
