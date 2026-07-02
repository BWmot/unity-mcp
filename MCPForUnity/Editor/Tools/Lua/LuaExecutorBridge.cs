using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace MCPForUnity.Editor.Tools.Lua
{
    public static class LuaExecutorBridge
    {
        public static LuaExecuteResult Execute(LuaExecuteRequest request)
        {
            var sw = Stopwatch.StartNew();

            if (request == null)
            {
                return Fail("request is null", "none", sw);
            }

            if (LuaMcpSettings.EnableLuaTool == false || request.mode == LuaExecuteMode.Disabled)
            {
                return Fail("Lua execution is disabled by settings.", "disabled", sw);
            }

            if (string.IsNullOrWhiteSpace(request.code))
            {
                return Fail("Lua code is empty.", "none", sw);
            }

            var mode = request.mode == LuaExecuteMode.CodeExecutorFirst && LuaMcpSettings.PreferredBackend != LuaExecuteMode.CodeExecutorFirst
                ? LuaMcpSettings.PreferredBackend
                : request.mode;

            if (mode == LuaExecuteMode.CodeExecutorFirst ||
                mode == LuaExecuteMode.CodeExecutorStandalone ||
                mode == LuaExecuteMode.CodeExecutorInGame ||
                mode == LuaExecuteMode.OnlyCodeExecutor)
            {
                if (!LuaMcpSettings.EnableCodeExecutorIntegration)
                {
                    return Fail("Code Executor integration is disabled by settings.", "code_executor", sw);
                }

                if (!IsCodeExecutorAvailable())
                {
                    return Fail($"Code Executor dependency is not installed. {MCPForUnity.Editor.Dependencies.CodeExecutorDependency.GetInstallHint()}", "code_executor", sw);
                }

                if (TryExecuteWithCodeExecutor(request, out var output, out var error, out var backendName))
                {
                    return Success(output, backendName, sw);
                }

                if (mode == LuaExecuteMode.OnlyCodeExecutor ||
                    mode == LuaExecuteMode.CodeExecutorStandalone ||
                    mode == LuaExecuteMode.CodeExecutorInGame)
                {
                    return Fail(error ?? "Code Executor execution failed.", "code_executor", sw);
                }
            }

            if ((mode == LuaExecuteMode.CodeExecutorFirst || mode == LuaExecuteMode.XLuaFirst) && LuaMcpSettings.AllowFallbackToXLua)
            {
                if (TryExecuteWithXLua(request.code, request.arguments, out var output, out var error))
                {
                    return Success(output, "xlua", sw);
                }
            }

            if ((mode == LuaExecuteMode.CodeExecutorFirst || mode == LuaExecuteMode.NLuaFirst) && LuaMcpSettings.AllowFallbackToNLua)
            {
                if (TryExecuteWithNLua(request.code, request.arguments, out var output, out var error))
                {
                    return Success(output, "nlua", sw);
                }
            }

            return Fail("No available Lua backend could execute the request.", "none", sw);
        }

        public static bool IsCodeExecutorInstalled()
        {
            return IsCodeExecutorAvailable();
        }

        public static bool IsCodeExecutorAvailable()
        {
            return MCPForUnity.Editor.Dependencies.CodeExecutorDependency.IsInstalled()
                   || FindType("CodeExecutorManager") != null
                   || FindType("CodeExecutorLuaApi") != null;
        }

        private static bool TryExecuteWithCodeExecutor(LuaExecuteRequest request, out string output, out string error, out string backendName)
        {
            output = null;
            error = null;
            backendName = "code_executor";

            var modeName = ResolveCodeExecutorModeName(request);
            if (!string.IsNullOrWhiteSpace(modeName))
            {
                if (string.Equals(modeName, "xLua (InGame)", StringComparison.OrdinalIgnoreCase) && !EditorApplication.isPlaying)
                {
                    error = "Code Executor execution mode 'xLua (InGame)' requires Play Mode. Please run the game in Unity first.";
                    backendName = "code_executor:" + modeName;
                    return false;
                }

                backendName = "code_executor:" + modeName;
                if (TryExecuteWithCodeExecutorMode(request.code, modeName, out output, out error))
                {
                    return true;
                }
            }

            var helperType = FindType("ChenPipi.CodeExecutor.Examples.ExecutionHelperXLua") ?? FindType("ExecutionHelperXLua");
            if (helperType != null)
            {
                if (TryInvokeStaticMethod(helperType, "ExecuteCode", request.code, out output, out error))
                {
                    return true;
                }

                if (TryInvokeStaticMethod(helperType, "Execute", request.code, out output, out error))
                {
                    return true;
                }

                if (TryInvokeStaticMethod(helperType, "Run", request.code, out output, out error))
                {
                    return true;
                }

                var method = helperType.GetMethod("ExecuteCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(string), typeof(object) }, null);
                if (method != null)
                {
                    try
                    {
                        var result = method.Invoke(null, new object[] { request.code, null });
                        output = FormatResult(result);
                        return true;
                    }
                    catch (TargetInvocationException tie)
                    {
                        error = tie.InnerException?.Message ?? tie.Message;
                        return false;
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                        return false;
                    }
                }
            }

            var managerType = FindType("CodeExecutorManager");
            if (managerType != null)
            {
                var method = FindExecuteMethod(managerType);
                if (method != null)
                {
                    if (TryInvokeStaticMethod(managerType, method.Name, request.code, out output, out error))
                    {
                        return true;
                    }
                }

                error = "CodeExecutorManager found but no stable Lua execution entry was exposed.";
            }

            error = error ?? "Code Executor backend not found.";
            return false;
        }

        private static string ResolveCodeExecutorModeName(LuaExecuteRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.codeExecutorMode))
            {
                return request.codeExecutorMode;
            }

            switch (request.mode)
            {
                case LuaExecuteMode.CodeExecutorStandalone:
                    return "xLua (Standalone)";
                case LuaExecuteMode.CodeExecutorInGame:
                    return "xLua (InGame)";
                default:
                    return null;
            }
        }

        private static bool TryExecuteWithCodeExecutorMode(string code, string modeName, out string output, out string error)
        {
            output = null;
            error = null;

            var managerType = FindType("ChenPipi.CodeExecutor.Editor.CodeExecutorManager") ?? FindType("CodeExecutorManager");
            if (managerType == null)
            {
                error = "CodeExecutorManager not found.";
                return false;
            }

            var hasMethod = managerType.GetMethod("HasExecMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
            if (hasMethod != null)
            {
                try
                {
                    var exists = hasMethod.Invoke(null, new object[] { modeName });
                    if (exists is bool boolExists && !boolExists)
                    {
                        error = $"Code Executor execution mode '{modeName}' is not registered.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    error = $"Failed to query Code Executor execution mode '{modeName}': {ex.Message}";
                    return false;
                }
            }

            var executeMethod = managerType.GetMethod("ExecuteCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
            if (executeMethod == null)
            {
                error = "CodeExecutorManager.ExecuteCode(string, string) not found.";
                return false;
            }

            try
            {
                var result = executeMethod.Invoke(null, new object[] { code, modeName });
                if (result == null)
                {
                    error = $"Code Executor execution mode '{modeName}' returned null.";
                    return false;
                }

                output = FormatResult(result);
                return true;
            }
            catch (TargetInvocationException tie)
            {
                error = tie.InnerException?.Message ?? tie.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryInvokeStaticMethod(Type type, string methodName, string code, out string output, out string error)
        {
            output = null;
            error = null;

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length < 1 || parameters.Length > 3)
                {
                    continue;
                }

                if (parameters[0].ParameterType != typeof(string) && parameters[0].ParameterType != typeof(object))
                {
                    continue;
                }

                try
                {
                    object result = null;
                    if (parameters.Length == 1)
                    {
                        result = method.Invoke(null, new object[] { code });
                    }
                    else if (parameters.Length == 2)
                    {
                        result = method.Invoke(null, new object[] { code, GetDefaultValue(parameters[1].ParameterType) });
                    }
                    else if (parameters.Length == 3)
                    {
                        result = method.Invoke(null, new object[] { code, GetDefaultValue(parameters[1].ParameterType), GetDefaultValue(parameters[2].ParameterType) });
                    }
                    else
                    {
                        continue;
                    }

                    output = FormatResult(result);
                    return true;
                }
                catch (TargetInvocationException tie)
                {
                    error = tie.InnerException?.Message ?? tie.Message;
                    return false;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }

            error = $"Method '{methodName}' not found on '{type.FullName}'.";
            return false;
        }

        private static object GetDefaultValue(Type type)
        {
            if (type == null)
            {
                return null;
            }

            if (!type.IsValueType)
            {
                return null;
            }

            return Activator.CreateInstance(type);
        }

        private static MethodInfo FindExecuteMethod(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var method in methods)
            {
                if (method.Name.IndexOf("execute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    method.Name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return method;
                }
            }

            return null;
        }

        private static bool TryExecuteWithXLua(string code, JObject arguments, out string output, out string error)
        {
            output = null;
            error = null;

            var luaEnvType = FindType("XLua.LuaEnv");
            if (luaEnvType == null)
            {
                error = "XLua.LuaEnv not found.";
                return false;
            }

            error = "XLua backend is present but no executor entry was wired.";
            return false;
        }

        private static bool TryExecuteWithNLua(string code, JObject arguments, out string output, out string error)
        {
            output = null;
            error = null;

            var luaType = FindType("NLua.Lua");
            if (luaType == null)
            {
                error = "NLua.Lua not found.";
                return false;
            }

            error = "NLua backend is present but no executor entry was wired.";
            return false;
        }

        private static LuaExecuteResult Success(string output, string backend, Stopwatch sw)
        {
            sw.Stop();
            return new LuaExecuteResult
            {
                success = true,
                output = output,
                error = null,
                backend = backend,
                durationMs = sw.ElapsedMilliseconds
            };
        }

        private static LuaExecuteResult Fail(string error, string backend, Stopwatch sw)
        {
            sw.Stop();
            return new LuaExecuteResult
            {
                success = false,
                output = null,
                error = error,
                backend = backend,
                durationMs = sw.ElapsedMilliseconds
            };
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(typeName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                    // ignored
                }
            }
            return null;
        }

        private static string FormatResult(object result)
        {
            if (result == null)
            {
                return null;
            }

            if (result is Array array)
            {
                var builder = new StringBuilder();
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(array.GetValue(i)?.ToString() ?? "nil");
                }

                return builder.ToString();
            }

            if (result is System.Collections.IEnumerable enumerable && !(result is string))
            {
                var builder = new StringBuilder();
                var first = true;
                foreach (var item in enumerable)
                {
                    if (!first)
                    {
                        builder.Append(", ");
                    }

                    first = false;
                    builder.Append(item?.ToString() ?? "nil");
                }

                return builder.ToString();
            }

            return result.ToString();
        }
    }
}
