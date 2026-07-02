using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Lua
{
    [McpForUnityTool("lua_execute", AutoRegister = true, Group = "deps")]
    public static class LuaExecuteTool
    {
        public sealed class Parameters
        {
            [ToolParameter("Lua source code to execute.", Required = true)]
            public string code { get; set; }

            [ToolParameter("Execution backend mode. Valid values: CodeExecutorFirst, CodeExecutorStandalone, CodeExecutorInGame, XLuaFirst, NLuaFirst, OnlyCodeExecutor, Disabled.", Required = false, DefaultValue = "CodeExecutorFirst")]
            public string mode { get; set; }

            [ToolParameter("Optional Code Executor mode name. Examples: xLua (Standalone), xLua (InGame). Overrides the default Code Executor mode selection.", Required = false)]
            public string codeExecutorMode { get; set; }

            [ToolParameter("Execution timeout in milliseconds. 0 means use the backend default.", Required = false, DefaultValue = "0")]
            public int timeoutMs { get; set; }

            [ToolParameter("Whether to request sandboxed execution when supported by the backend.", Required = false, DefaultValue = "false")]
            public bool useSandbox { get; set; }

            [ToolParameter("Optional arguments object passed to the Lua execution backend.", Required = false)]
            public object arguments { get; set; }
        }

        public static Task<object> HandleCommand(JObject @params)
        {
            try
            {
                if (@params == null)
                {
                    return Task.FromResult<object>(new ErrorResponse("Parameters cannot be null."));
                }

                var p = new ToolParams(@params);
                var codeResult = p.GetRequired("code", "'code' parameter is required.");
                if (!codeResult.IsSuccess)
                {
                    return Task.FromResult<object>(new ErrorResponse(codeResult.ErrorMessage));
                }

                var request = new LuaExecuteRequest
                {
                    code = codeResult.Value,
                    mode = ParseMode(p.Get("mode")),
                    timeoutMs = p.GetInt("timeoutMs") ?? 0,
                    useSandbox = p.GetBool("useSandbox"),
                    codeExecutorMode = p.Get("codeExecutorMode"),
                    arguments = @params["arguments"] as JObject
                };

                var result = LuaExecutorBridge.Execute(request);
                if (result.success)
                {
                    return Task.FromResult<object>(new SuccessResponse("Lua executed successfully.", new
                    {
                        backend = result.backend,
                        output = result.output,
                        duration_ms = result.durationMs
                    }));
                }

                return Task.FromResult<object>(new ErrorResponse(result.error ?? "Lua execution failed.", new
                {
                    backend = result.backend,
                    duration_ms = result.durationMs
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new ErrorResponse($"Failed to execute Lua: {ex.Message}", new { stackTrace = ex.StackTrace }));
            }
        }

        private static LuaExecuteMode ParseMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return LuaExecuteMode.CodeExecutorFirst;
            }

            if (Enum.TryParse(mode, true, out LuaExecuteMode parsed))
            {
                return parsed;
            }

            return LuaExecuteMode.CodeExecutorFirst;
        }
    }
}
