using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Dependencies;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Lua
{
    [McpForUnityTool("lua_backend_status", AutoRegister = true, Group = "scripting_ext")]
    public static class LuaBackendStatusTool
    {
        public static Task<object> HandleCommand(JObject @params)
        {
            try
            {
                var data = new
                {
                    toolName = "lua_backend_status",
                    luaToolEnabled = LuaMcpSettings.EnableLuaTool,
                    codeExecutorIntegrationEnabled = LuaMcpSettings.EnableCodeExecutorIntegration,
                    preferredBackend = LuaMcpSettings.PreferredBackend.ToString(),
                    allowFallbackToXLua = LuaMcpSettings.AllowFallbackToXLua,
                    allowFallbackToNLua = LuaMcpSettings.AllowFallbackToNLua,
                    codeExecutorInstalled = LuaExecutorBridge.IsCodeExecutorAvailable(),
                    codeExecutorInstallHint = CodeExecutorDependency.GetInstallHint(),
                    codeExecutorDetectedSource = CodeExecutorDependency.GetDetectedSource(),
                    luaExecuteToolAvailable = true
                };

                return Task.FromResult<object>(new SuccessResponse("Lua backend status queried.", data));
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new ErrorResponse($"Failed to query Lua backend status: {ex.Message}", new { stackTrace = ex.StackTrace }));
            }
        }
    }
}
