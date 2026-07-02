using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Lua
{
    public enum LuaExecuteMode
    {
        CodeExecutorFirst,
        CodeExecutorStandalone,
        CodeExecutorInGame,
        XLuaFirst,
        NLuaFirst,
        OnlyCodeExecutor,
        Disabled
    }

    public sealed class LuaExecuteRequest
    {
        public string code;
        public LuaExecuteMode mode = LuaExecuteMode.CodeExecutorFirst;
        public int timeoutMs = 0;
        public JObject arguments;
        public string codeExecutorMode;
        public bool useSandbox = false;
    }

    public sealed class LuaExecuteResult
    {
        public bool success;
        public string output;
        public string error;
        public string backend;
        public long durationMs;
    }
}
