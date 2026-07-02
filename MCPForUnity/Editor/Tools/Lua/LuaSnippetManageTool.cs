using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;

namespace MCPForUnity.Editor.Tools.Lua
{
    /// <summary>
    /// Manage persistent Code Executor snippets/categories from MCP.
    /// Uses reflection intentionally so MCPForUnity.Editor does not need a hard asmdef reference
    /// to ChenPipi.CodeExecutor.Editor.
    /// </summary>
    [McpForUnityTool("lua_snippet_manage", AutoRegister = true, Group = "scripting_ext")]
    public static class LuaSnippetManageTool
    {
        private const string DefaultLuaMode = "xLua (InGame)";

        public sealed class Parameters
        {
            [ToolParameter("Action. Valid values: list, create_category, delete_category, rename_category, create_snippet, read_snippet, update_snippet, delete_snippet, move_snippet.", Required = true)]
            public string action { get; set; }

            [ToolParameter("Snippet guid for read/update/delete/move operations.", Required = false)]
            public string guid { get; set; }

            [ToolParameter("Snippet or category name. For category operations this can be used instead of category.", Required = false)]
            public string name { get; set; }

            [ToolParameter("New name for rename/update operations.", Required = false)]
            public string newName { get; set; }

            [ToolParameter("Category/folder name. Empty string moves a snippet to uncategorized when supported by the action.", Required = false)]
            public string category { get; set; }

            [ToolParameter("Lua/code snippet content.", Required = false)]
            public string code { get; set; }

            [ToolParameter("Code Executor mode name. Examples: xLua (InGame), xLua (Standalone), C#, None.", Required = false, DefaultValue = DefaultLuaMode)]
            public string mode { get; set; }

            [ToolParameter("Whether to include full code content in list output.", Required = false, DefaultValue = "false")]
            public bool includeCode { get; set; }

            [ToolParameter("Overwrite an existing snippet with the same name/category/mode on create_snippet.", Required = false, DefaultValue = "false")]
            public bool overwrite { get; set; }

            [ToolParameter("For delete_category, also delete snippets under it. If false and the category is not empty, the action fails unless detachSnippets is true.", Required = false, DefaultValue = "false")]
            public bool recursive { get; set; }

            [ToolParameter("For delete_category, keep snippets and move them to uncategorized by deleting only the category. Safer alternative to recursive deletion.", Required = false, DefaultValue = "false")]
            public bool detachSnippets { get; set; }

            [ToolParameter("Snippet pinned/top state for update_snippet.", Required = false, DefaultValue = "false")]
            public bool top { get; set; }
        }

        public static Task<object> HandleCommand(JObject @params)
        {
            try
            {
                if (@params == null)
                {
                    return Task.FromResult<object>(new ErrorResponse("Parameters cannot be null."));
                }

                if (!LuaExecutorBridge.IsCodeExecutorAvailable())
                {
                    return Task.FromResult<object>(new ErrorResponse("Code Executor dependency is not installed or not loaded."));
                }

                var p = new ToolParams(@params);
                var actionResult = p.GetRequired("action", "'action' parameter is required.");
                if (!actionResult.IsSuccess)
                {
                    return Task.FromResult<object>(new ErrorResponse(actionResult.ErrorMessage));
                }

                var bridge = CodeExecutorSnippetBridge.Create();
                if (!bridge.IsAvailable)
                {
                    return Task.FromResult<object>(new ErrorResponse(bridge.Error));
                }

                var action = NormalizeAction(actionResult.Value);
                switch (action)
                {
                    case "list":
                        return Task.FromResult<object>(new SuccessResponse("Code Executor snippets listed.", BuildListData(bridge, p.GetBool("includeCode"))));

                    case "create_category":
                        return Task.FromResult<object>(CreateCategory(bridge, p));

                    case "delete_category":
                        return Task.FromResult<object>(DeleteCategory(bridge, p));

                    case "rename_category":
                        return Task.FromResult<object>(RenameCategory(bridge, p));

                    case "create_snippet":
                        return Task.FromResult<object>(CreateSnippet(bridge, p));

                    case "read_snippet":
                        return Task.FromResult<object>(ReadSnippet(bridge, p));

                    case "update_snippet":
                        return Task.FromResult<object>(UpdateSnippet(bridge, p));

                    case "delete_snippet":
                        return Task.FromResult<object>(DeleteSnippet(bridge, p));

                    case "move_snippet":
                        return Task.FromResult<object>(MoveSnippet(bridge, p));

                    default:
                        return Task.FromResult<object>(new ErrorResponse($"Unsupported action '{actionResult.Value}'."));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new ErrorResponse($"Failed to manage Lua snippets: {ex.Message}", new { stackTrace = ex.StackTrace }));
            }
        }

        private static object CreateCategory(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var category = GetCategoryName(p);
            if (string.IsNullOrWhiteSpace(category))
            {
                return new ErrorResponse("'name' or 'category' is required for create_category.");
            }

            if (bridge.HasCategory(category))
            {
                return new SuccessResponse("Category already exists.", new { category });
            }

            bridge.AddCategory(category, true);
            return new SuccessResponse("Category created.", new { category });
        }

        private static object DeleteCategory(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var category = GetCategoryName(p);
            if (string.IsNullOrWhiteSpace(category))
            {
                return new ErrorResponse("'name' or 'category' is required for delete_category.");
            }

            if (!bridge.HasCategory(category))
            {
                return new ErrorResponse($"Category '{category}' does not exist.");
            }

            var snippets = bridge.GetSnippetsWithCategory(category).ToList();
            var recursive = p.GetBool("recursive");
            var detachSnippets = p.GetBool("detachSnippets") || p.GetBool("detach_snippets");
            if (snippets.Count > 0 && !recursive && !detachSnippets)
            {
                return new ErrorResponse($"Category '{category}' contains {snippets.Count} snippet(s). Set recursive=true to delete them, or detachSnippets=true to keep them uncategorized.", new
                {
                    category,
                    snippets = snippets.Select(s => ToSnippetDto(s, includeCode: false)).ToArray()
                });
            }

            if (recursive)
            {
                bridge.RemoveSnippetsWithCategory(category, false);
            }

            // Code Executor's RemoveCategory keeps snippets by clearing their category when recursive=false/detach=true.
            bridge.RemoveCategory(category, true);
            return new SuccessResponse(recursive ? "Category and snippets deleted." : "Category deleted; snippets were kept uncategorized.", new
            {
                category,
                deletedSnippetCount = recursive ? snippets.Count : 0,
                detachedSnippetCount = recursive ? 0 : snippets.Count
            });
        }

        private static object RenameCategory(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var category = GetCategoryName(p);
            var newName = p.Get("newName") ?? p.Get("new_name");
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(newName))
            {
                return new ErrorResponse("'name'/'category' and 'newName' are required for rename_category.");
            }

            if (!bridge.HasCategory(category))
            {
                return new ErrorResponse($"Category '{category}' does not exist.");
            }

            if (bridge.HasCategory(newName))
            {
                return new ErrorResponse($"Category '{newName}' already exists.");
            }

            bridge.RenameCategory(category, newName);
            return new SuccessResponse("Category renamed.", new { category = newName, oldCategory = category });
        }

        private static object CreateSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var code = p.Get("code");
            if (code == null)
            {
                return new ErrorResponse("'code' is required for create_snippet.");
            }

            var name = p.Get("name");
            var category = p.Get("category");
            var mode = p.Get("mode", DefaultLuaMode);
            var overwrite = p.GetBool("overwrite");

            if (!string.IsNullOrWhiteSpace(category) && !bridge.HasCategory(category))
            {
                bridge.AddCategory(category, false);
            }

            var existing = !string.IsNullOrWhiteSpace(name)
                ? bridge.FindSnippetByNameCategoryMode(name, category, mode)
                : null;
            if (existing != null)
            {
                if (!overwrite)
                {
                    return new ErrorResponse($"Snippet '{name}' already exists in category '{category ?? string.Empty}' with mode '{mode}'. Set overwrite=true to update it.", ToSnippetDto(existing, includeCode: false));
                }

                bridge.SetSnippetCode(existing.Guid, code);
                bridge.SetSnippetExecMode(existing.Guid, mode);
                bridge.SetSnippetCategory(existing.Guid, category, false);
                bridge.ReloadData(true);
                return new SuccessResponse("Snippet overwritten.", ToSnippetDto(bridge.GetSnippet(existing.Guid), includeCode: true));
            }

            var snippet = bridge.AddSnippet(code, name, mode, category, true);
            return new SuccessResponse("Snippet created.", ToSnippetDto(snippet, includeCode: true));
        }

        private static object ReadSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var snippet = ResolveSnippet(bridge, p);
            if (snippet == null)
            {
                return new ErrorResponse("Snippet not found. Provide 'guid', or 'name' with optional 'category'/'mode'.");
            }

            return new SuccessResponse("Snippet read.", ToSnippetDto(snippet, includeCode: true));
        }

        private static object UpdateSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var snippet = ResolveSnippet(bridge, p);
            if (snippet == null)
            {
                return new ErrorResponse("Snippet not found. Provide 'guid', or 'name' with optional 'category'/'mode'.");
            }

            var guid = snippet.Guid;
            if (p.Has("newName") || p.Has("new_name"))
            {
                bridge.SetSnippetName(guid, p.Get("newName") ?? p.Get("new_name"));
            }
            else if (p.Has("name") && !p.Has("guid"))
            {
                // When updating by guid, 'name' is often a lookup parameter. Avoid changing it unless newName is used.
                bridge.SetSnippetName(guid, p.Get("name"));
            }

            if (p.Has("code"))
            {
                bridge.SetSnippetCode(guid, p.Get("code"));
            }

            if (p.Has("mode"))
            {
                bridge.SetSnippetExecMode(guid, p.Get("mode"));
            }

            if (p.Has("category"))
            {
                var category = p.Get("category");
                if (!string.IsNullOrWhiteSpace(category) && !bridge.HasCategory(category))
                {
                    bridge.AddCategory(category, false);
                }
                bridge.SetSnippetCategory(guid, category, false);
            }

            if (p.Has("top"))
            {
                bridge.SetSnippetTop(guid, p.GetBool("top"), false);
            }

            bridge.ReloadData(true);
            return new SuccessResponse("Snippet updated.", ToSnippetDto(bridge.GetSnippet(guid), includeCode: true));
        }

        private static object DeleteSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var snippet = ResolveSnippet(bridge, p);
            if (snippet == null)
            {
                return new ErrorResponse("Snippet not found. Provide 'guid', or 'name' with optional 'category'/'mode'.");
            }

            var dto = ToSnippetDto(snippet, includeCode: false);
            bridge.RemoveSnippet(snippet.Guid);
            return new SuccessResponse("Snippet deleted.", dto);
        }

        private static object MoveSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var snippet = ResolveSnippet(bridge, p);
            if (snippet == null)
            {
                return new ErrorResponse("Snippet not found. Provide 'guid', or 'name' with optional 'category'/'mode'.");
            }

            if (!p.Has("category"))
            {
                return new ErrorResponse("'category' is required for move_snippet. Use an empty string to move to uncategorized.");
            }

            var category = p.Get("category");
            if (!string.IsNullOrWhiteSpace(category) && !bridge.HasCategory(category))
            {
                bridge.AddCategory(category, false);
            }

            bridge.SetSnippetCategory(snippet.Guid, category, true);
            return new SuccessResponse("Snippet moved.", ToSnippetDto(bridge.GetSnippet(snippet.Guid), includeCode: true));
        }

        private static object BuildListData(CodeExecutorSnippetBridge bridge, bool includeCode)
        {
            var snippets = bridge.GetSnippets().ToList();
            var categories = bridge.GetCategories()
                .Select(category => new
                {
                    name = category,
                    snippetCount = snippets.Count(s => string.Equals(s.Category ?? string.Empty, category ?? string.Empty, StringComparison.Ordinal))
                })
                .ToArray();

            return new
            {
                categories,
                snippets = snippets.Select(s => ToSnippetDto(s, includeCode)).ToArray()
            };
        }

        private static SnippetRecord ResolveSnippet(CodeExecutorSnippetBridge bridge, ToolParams p)
        {
            var guid = p.Get("guid");
            if (!string.IsNullOrWhiteSpace(guid))
            {
                return bridge.GetSnippet(guid);
            }

            var name = p.Get("name");
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return bridge.FindSnippetByNameCategoryMode(name, p.Get("category"), p.Get("mode"));
        }

        private static object ToSnippetDto(SnippetRecord snippet, bool includeCode)
        {
            if (snippet == null)
            {
                return null;
            }

            return new
            {
                guid = snippet.Guid,
                name = snippet.Name,
                category = snippet.Category,
                mode = snippet.Mode,
                top = snippet.Top,
                createTime = snippet.CreateTime,
                editTime = snippet.EditTime,
                codeLength = snippet.Code == null ? 0 : snippet.Code.Length,
                code = includeCode ? snippet.Code : null
            };
        }

        private static string GetCategoryName(ToolParams p)
        {
            return p.Get("category") ?? p.Get("name");
        }

        private static string NormalizeAction(string action)
        {
            return string.IsNullOrWhiteSpace(action)
                ? string.Empty
                : action.Trim().Replace('-', '_').ToLowerInvariant();
        }

        private sealed class CodeExecutorSnippetBridge
        {
            private readonly Type _managerType;
            private readonly Type _dataType;
            private readonly string _error;

            public bool IsAvailable => _managerType != null && _dataType != null;
            public string Error => _error;

            private CodeExecutorSnippetBridge(Type managerType, Type dataType, string error)
            {
                _managerType = managerType;
                _dataType = dataType;
                _error = error;
            }

            public static CodeExecutorSnippetBridge Create()
            {
                var managerType = FindType("ChenPipi.CodeExecutor.Editor.CodeExecutorManager") ?? FindType("CodeExecutorManager");
                var dataType = FindType("ChenPipi.CodeExecutor.Editor.CodeExecutorData") ?? FindType("CodeExecutorData");
                if (managerType == null || dataType == null)
                {
                    return new CodeExecutorSnippetBridge(managerType, dataType, "CodeExecutorManager or CodeExecutorData was not found.");
                }

                return new CodeExecutorSnippetBridge(managerType, dataType, null);
            }

            public IEnumerable<string> GetCategories()
            {
                return Invoke(_dataType, "GetCategories") is IEnumerable enumerable
                    ? enumerable.Cast<object>().Select(o => o?.ToString()).Where(s => !string.IsNullOrEmpty(s)).ToArray()
                    : Array.Empty<string>();
            }

            public bool HasCategory(string name)
            {
                return Invoke(_dataType, "HasCategory", name) is bool value && value;
            }

            public void AddCategory(string name, bool notify)
            {
                Invoke(_managerType, "AddCategory", name, notify);
            }

            public void RemoveCategory(string name, bool notify)
            {
                Invoke(_managerType, "RemoveCategory", name, notify);
            }

            public void RenameCategory(string originalName, string newName)
            {
                Invoke(_managerType, "RenameCategory", originalName, newName);
            }

            public IEnumerable<SnippetRecord> GetSnippets()
            {
                return Invoke(_dataType, "GetSnippets") is IEnumerable enumerable
                    ? enumerable.Cast<object>().Select(SnippetRecord.FromObject).Where(s => s != null).ToArray()
                    : Array.Empty<SnippetRecord>();
            }

            public IEnumerable<SnippetRecord> GetSnippetsWithCategory(string category)
            {
                return Invoke(_dataType, "GetSnippetsWithCategory", category) is IEnumerable enumerable
                    ? enumerable.Cast<object>().Select(SnippetRecord.FromObject).Where(s => s != null).ToArray()
                    : Array.Empty<SnippetRecord>();
            }

            public SnippetRecord GetSnippet(string guid)
            {
                if (string.IsNullOrWhiteSpace(guid))
                {
                    return null;
                }

                return SnippetRecord.FromObject(Invoke(_dataType, "GetSnippet", guid));
            }

            public SnippetRecord FindSnippetByNameCategoryMode(string name, string category, string mode)
            {
                return GetSnippets().FirstOrDefault(s =>
                    string.Equals(s.Name ?? string.Empty, name ?? string.Empty, StringComparison.Ordinal) &&
                    string.Equals(s.Category ?? string.Empty, category ?? string.Empty, StringComparison.Ordinal) &&
                    (string.IsNullOrWhiteSpace(mode) || string.Equals(s.Mode ?? string.Empty, mode ?? string.Empty, StringComparison.OrdinalIgnoreCase)));
            }

            public SnippetRecord AddSnippet(string code, string name, string mode, string category, bool notify)
            {
                return SnippetRecord.FromObject(Invoke(_managerType, "AddSnippet", code, name, mode, category, notify));
            }

            public void RemoveSnippet(string guid)
            {
                Invoke(_managerType, "RemoveSnippet", guid);
            }

            public void RemoveSnippetsWithCategory(string category, bool notify)
            {
                Invoke(_managerType, "RemoveSnippetsWithCategory", category, notify);
            }

            public void SetSnippetName(string guid, string name)
            {
                Invoke(_managerType, "SetSnippetName", guid, name);
            }

            public void SetSnippetCode(string guid, string code)
            {
                Invoke(_managerType, "SetSnippetCode", guid, code);
            }

            public void SetSnippetExecMode(string guid, string mode)
            {
                Invoke(_managerType, "SetSnippetExecMode", guid, mode);
            }

            public void SetSnippetCategory(string guid, string category, bool notify)
            {
                Invoke(_managerType, "SetSnippetCategory", guid, category, notify);
            }

            public void SetSnippetTop(string guid, bool top, bool notify)
            {
                Invoke(_managerType, "SetSnippetTop", guid, top, notify);
            }

            public void ReloadData(bool notify)
            {
                Invoke(_managerType, "ReloadData", notify);
            }

            private static object Invoke(Type type, string methodName, params object[] args)
            {
                var argTypes = args.Select(a => a?.GetType()).ToArray();
                var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m =>
                    {
                        if (!string.Equals(m.Name, methodName, StringComparison.Ordinal)) return false;
                        var parameters = m.GetParameters();
                        if (parameters.Length != args.Length) return false;
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (args[i] == null) continue;
                            if (!parameters[i].ParameterType.IsAssignableFrom(argTypes[i])) return false;
                        }
                        return true;
                    });

                if (method == null)
                {
                    throw new MissingMethodException(type.FullName, methodName);
                }

                try
                {
                    return method.Invoke(null, args);
                }
                catch (TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }
            }

            private static Type FindType(string typeName)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type type = null;
                    try
                    {
                        type = assembly.GetType(typeName, false);
                    }
                    catch
                    {
                        // ignored
                    }

                    if (type != null)
                    {
                        return type;
                    }
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }
                    catch
                    {
                        continue;
                    }

                    var match = types.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
                    if (match != null)
                    {
                        return match;
                    }
                }

                return null;
            }
        }

        private sealed class SnippetRecord
        {
            public string Guid { get; private set; }
            public string Name { get; private set; }
            public string Code { get; private set; }
            public string Mode { get; private set; }
            public string Category { get; private set; }
            public bool Top { get; private set; }
            public long CreateTime { get; private set; }
            public long EditTime { get; private set; }

            public static SnippetRecord FromObject(object source)
            {
                if (source == null)
                {
                    return null;
                }

                return new SnippetRecord
                {
                    Guid = GetFieldValue<string>(source, "guid"),
                    Name = GetFieldValue<string>(source, "name"),
                    Code = GetFieldValue<string>(source, "code"),
                    Mode = GetFieldValue<string>(source, "mode"),
                    Category = GetFieldValue<string>(source, "category"),
                    Top = GetFieldValue<bool>(source, "top"),
                    CreateTime = GetFieldValue<long>(source, "createTime"),
                    EditTime = GetFieldValue<long>(source, "editTime")
                };
            }

            private static T GetFieldValue<T>(object source, string name)
            {
                var field = source.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    return default;
                }

                var value = field.GetValue(source);
                if (value == null)
                {
                    return default;
                }

                if (value is T typed)
                {
                    return typed;
                }

                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
        }
    }
}
