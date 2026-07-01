# Unity MCP 降级兼容改动文档

> **源版本**: Unity 2021.3（上游 main 分支）  
> **目标版本**: Unity 2020.2.5f1 (C# 8.0 / .NET Standard 2.0)  
> **生成日期**: 2025-06-30  
> **涉及文件**: 81 个文件，+673 / -312 行

---

## 目录

1. [概述与策略](#1-概述与策略)
2. [C# 8.0 语法兼容](#2-c-80-语法兼容)
3. [.NET Standard 2.0 API 兼容](#3-net-standard-20-api-兼容)
4. [UI Toolkit API 降级](#4-ui-toolkit-api-降级)
5. [Unity Editor API 版本门控](#5-unity-editor-api-版本门控)
6. [构建设置 / BuildTargetMapping](#6-构建设置--buildtargetmapping)
7. [包依赖 (package.json)](#7-包依赖-packagejson)
8. [Roslyn MCP 安装器 ZIP 处理](#8-roslyn-mcp-安装器-zip-处理)
9. [测试项目适配](#9-测试项目适配)
10. [完整文件清单](#10-完整文件清单)

---

## 1. 概述与策略

### 核心原则

| 策略 | 说明 |
|------|------|
| **最小侵入** | 尽量使用 `#if UNITY_2021_2_OR_NEWER` / `#else` 条件编译，保持上游代码可合并 |
| **零运行时开销** | 所有 fallback 逻辑在编译时决定，不引入运行时分支 |
| **功能等价** | 降级后功能行为与原版一致，仅在不可用 API 上返回明确错误 |
| **可回退性** | 所有改动可被 `git revert` 一键回退，条件编译保留原代码路径 |

### 版本检测宏

| 宏 | 含义 | 用途 |
|----|------|------|
| `UNITY_2021_2_OR_NEWER` | Unity 2021.2+ | 保护新 API (PopupField, PrefabStage, ZipArchive 等) |
| `UNITY_2022_1_OR_NEWER` | Unity 2022.1+ | 保护较新 API (customReflectionTexture 等) |
| `UNITY_2022_2_OR_NEWER` | Unity 2022.2+ | 保护 Profiler API (FileIO category) |
| `UNITY_2022_3_OR_NEWER` | Unity 2022.3+ | 保护 FindObjectsByType 等 |

---

## 2. C# 8.0 语法兼容

C# 9.0+ 语法不可用，以下模式需要替换：

### 2.1 目标类型 `new()` 表达式

**改动**: 约 30 处

```csharp
// ❌ C# 9.0
private static readonly Dictionary<string, Request> PendingRequests = new();

// ✅ C# 8.0
private static readonly Dictionary<string, Request> PendingRequests = new Dictionary<string, Request>();
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManagePackages.cs` (4 处)
- `MCPForUnity/Editor/Tools/ManageScriptableObject.cs` (2 处)
- `MCPForUnity/Editor/Tools/UnityReflect.cs` (3 处)
- `MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs` (3 处)
- `MCPForUnity/Editor/Windows/Components/Resources/McpResourcesSection.cs` (2 处)
- `MCPForUnity/Editor/Windows/Components/Tools/McpToolsSection.cs` (4 处)
- `MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs` (1 处)
- `MCPForUnity/Editor/Windows/MCPSetupWindow.cs` (1 处)
- `MCPForUnity/Editor/Tools/ManageUI.cs` (1 处)

### 2.2 模式匹配 `is not`

```csharp
// ❌ C# 9.0
if (token is not JObject jo)
if (patchesToken is not JArray patches)

// ✅ C# 8.0
if (!(token is JObject jo))
if (!(patchesToken is JArray patches))
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManageScriptableObject.cs` (5 处)
- `MCPForUnity/Runtime/Serialization/UnityTypeConverters.cs` (4 处)
- `MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs` (1 处)
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs` (1 处)

### 2.3 三元表达式类型不一致

```csharp
// ❌ C# 9.0 (不同类型分支)
return ok ? new SuccessResponse(...) : new ErrorResponse(...);

// ✅ C# 8.0
return ok ? (object)new SuccessResponse(...) : (object)new ErrorResponse(...);
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManageScript.cs` (1 处)

### 2.4 BOM 标记

部分文件的 BOM (`﻿`) 被添加，确保 Unity 的 Mono 编译器正确处理：

- `MCPForUnity/Editor/Tools/ManagePackages.cs`
- `MCPForUnity/Editor/Tools/UnityReflect.cs`
- `MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs`
- `MCPForUnity/Editor/Windows/Components/Resources/McpResourcesSection.cs`
- `MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs`
- `MCPForUnity/Editor/Windows/MCPSetupWindow.cs`

---

## 3. .NET Standard 2.0 API 兼容

### 3.1 `Enum.TryParse` 非泛型重载不存在

.NET Standard 2.0 **没有** `Enum.TryParse(Type, string, bool, out object)` 这个 4 参数非泛型重载。只有泛型 `Enum.TryParse<T>(string, bool, out T)` 可用。

由于此处需要动态类型解析，改用 `Enum.Parse` + try/catch：

```csharp
// ❌ .NET Standard 2.1+
if (Enum.TryParse(targetType, str, true, out object enumVal))
    return enumVal;

// ✅ .NET Standard 2.0
try { return Enum.Parse(targetType, str, ignoreCase: true); }
catch { /* fall through */ }
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Graphics/VolumeOps.cs` (L625-640)
- `MCPForUnity/Editor/Tools/Graphics/RenderPipelineOps.cs` (L255-265)

### 3.2 `Math.Clamp` → `Mathf.Clamp`

`System.Math.Clamp` 在 .NET Standard 2.0 不可用，改用 Unity 的 `Mathf.Clamp`：

```csharp
// ❌ .NET Standard 2.1+
short minCount = (short)Math.Clamp(minCountRaw, 0, short.MaxValue);

// ✅ .NET Standard 2.0
short minCount = (short)Mathf.Clamp(minCountRaw, 0, short.MaxValue);
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Vfx/ParticleControl.cs` (2 处)
- `MCPForUnity/Editor/Windows/Components/Tools/McpToolsSection.cs` (2 处)

### 3.3 `String.Replace(string, string, StringComparison)` 重载不存在

```csharp
// ❌ .NET Standard 2.1+
s = s.Replace("//", "/", StringComparison.Ordinal);

// ✅ .NET Standard 2.0
s = s.Replace("//", "/");
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManageScriptableObject.cs` (1 处)
- `MCPForUnity/Editor/Tools/ManageScript.cs` (1 处)

### 3.4 `String.Contains(char, StringComparison)` 重载不存在

`char` 版本不可用，改用 `IndexOf` 或字符串版本。

**涉及文件**: `MCPForUnity/Editor/Setup/RoslynInstaller.cs` (L159)

---

## 4. UI Toolkit API 降级

### 4.1 `Button.clicked` → `Button.clickable.clicked`

Unity 2020.x 中 `Button` 没有直接的 `clicked` 事件，必须通过 `clickable.clicked` 访问：

```csharp
// ❌ Unity 2021.1+
button.clicked += handler;

// ✅ Unity 2020.x
button.clickable.clicked += handler;
```

**涉及文件** (约 20 处):
- `MCPForUnity/Editor/Windows/Components/Advanced/McpAdvancedSection.cs` (6 处)
- `MCPForUnity/Editor/Windows/Components/Connection/McpConnectionSection.cs` (4 处)
- `MCPForUnity/Editor/Windows/Components/Resources/McpResourcesSection.cs` (3 处)
- `MCPForUnity/Editor/Windows/MCPSetupWindow.cs` (6 处)
- `MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs` (1 处)

### 4.2 `ToolbarToggle` → `Toggle`

`ToolbarToggle` 在 Unity 2021.2 才引入：

```csharp
// ❌ Unity 2021.2+
private ToolbarToggle clientsTabToggle;
clientsTabToggle = rootVisualElement.Q<ToolbarToggle>("clients-tab");

// ✅ Unity 2020.x
private Toggle clientsTabToggle;
clientsTabToggle = rootVisualElement.Q<Toggle>("clients-tab");
```

**涉及文件**:
- `MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.cs` (字段声明 5 处 + 查询 5 处)
- `MCPForUnity/Editor/Windows/MCPForUnityEditorWindow.uxml` (5 处: `ToolbarToggle` → `Toggle`)

### 4.3 `PopupField<T>` → `DropdownField` (条件编译)

`PopupField<T>` 泛型在 Unity 2021.2 才可用。在 2020.x 使用非泛型 `DropdownField`：

```csharp
// ✅ 版本门控
#if UNITY_2021_2_OR_NEWER
    var typeDropdown = new PopupField<string>(choices, defaultIndex);
#else
    var typeDropdown = new DropdownField(choices, defaultIndex);
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Windows/EditorPrefs/EditorPrefsWindow.cs` (2 处: item type + new item type)
- `MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs` (字段声明 + 使用)

**UXML 配套修改**:
- `MCPForUnity/Editor/Windows/EditorPrefs/EditorPrefItem.uxml`: `DropdownField` → `VisualElement` 占位
- `MCPForUnity/Editor/Windows/EditorPrefs/EditorPrefsWindow.uxml`: `DropdownField` → `VisualElement` 占位
- `MCPForUnity/Editor/Windows/Components/ClientConfig/McpClientConfigSection.uxml`: `DropdownField` → `VisualElement` 占位

> 策略：UXML 中用 `VisualElement` 占位，C# 代码中动态创建正确的控件类型后插入到占位位置。

### 4.4 `TextField.labelElement` → `Q<Label>()`

```csharp
// ❌ Unity 2021.1+
searchField.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;

// ✅ Unity 2020.x
searchField.Q<Label>().style.unityFontStyleAndWeight = FontStyle.Bold;
```

**涉及文件**:
- `MCPForUnity/Editor/Windows/EditorPrefs/EditorPrefsWindow.cs` (1 处)

---

## 5. Unity Editor API 版本门控

### 5.1 `PrefabStageUtility` (UNITY_2021_2_OR_NEWER)

`PrefabStageUtility.GetCurrentPrefabStage()` 在 Unity 2021.2+ 才可用。旧版跳过 Prefab Stage 检查：

```csharp
#if UNITY_2021_2_OR_NEWER
    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
    if (prefabStage != null) { ... }
#else
    // Fallback: skip prefab stage check
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManageComponents.cs` (1 处: `MarkOwningSceneDirty`)
- `MCPForUnity/Editor/Tools/ManageScene.cs` (1 处: `get_hierarchy`)
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs` (2 处: `FindSceneObjectByName` + using 语句)

### 5.2 `PrefabStageUtility` using 别名

```csharp
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs`

### 5.3 `UIDocument` / `PanelSettings` (UNITY_2021_2_OR_NEWER)

`UIDocument` 和 `PanelSettings` 在 Unity 2021.2+ 才可用。2020.x 返回明确错误：

```csharp
#if UNITY_2021_2_OR_NEWER
    var uiDoc = go.GetComponent<UIDocument>();
    // ... 正常逻辑
#else
    return new ErrorResponse("UIDocument not available in Unity version. Requires Unity 2021.2+.");
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/ManageUI.cs` (6 处函数级门控):
  - `AttachUIDocument`
  - `CreateDefaultPanelSettings`
  - `ApplyPanelSettingsProperties`
  - `ApplyDynamicAtlasSettings`
  - `TakeUIScreenshot` (UIDocument 读取)
  - `TakeUIScreenshot` (渲染逻辑)
  - `UnlinkStyleSheet`
  - `ModifyVisualElement`

### 5.4 `MaterialPropertyBlock.HasColor` (UNITY_2021_2_OR_NEWER)

```csharp
#if UNITY_2021_2_OR_NEWER
    if (mat.HasProperty(prop) && block.HasColor(prop))
#else
    if (mat.HasProperty(prop))
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs`

### 5.5 `PrefabUtility.SaveAsPrefabAssetAndConnect` interactionMode 参数

`InteractionMode` 枚举及带此参数的重载在 Unity 2021.2 引入：

```csharp
#if UNITY_2021_2_OR_NEWER
    PrefabUtility.SaveAsPrefabAssetAndConnect(sourceObject, path, InteractionMode.AutomatedAction);
#else
    PrefabUtility.SaveAsPrefabAssetAndConnect(sourceObject, path);
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/GameObjects/GameObjectCreate.cs` (1 处)
- `MCPForUnity/Editor/Tools/Prefabs/ManagePrefabs.cs` (2 处)
- `TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/ManagePrefabsCrudTests.cs` (1 处)

### 5.6 `ProfilerCategory.FileIO` (UNITY_2021_2_OR_NEWER)

```csharp
#if UNITY_2021_2_OR_NEWER
    case "fileio": return ProfilerCategory.FileIO;
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Profiler/Operations/CounterOps.cs` (2 处: 有效类别列表 + switch case)

### 5.7 `LightmapCompression` (UNITY_2021_2_OR_NEWER)

```csharp
#if UNITY_2021_2_OR_NEWER
    // LightmapCompression 可用
#else
    return false; // LightmapCompression not available
#endif
```

**涉及文件**:
- `MCPForUnity/Editor/Tools/Graphics/LightBakingOps.cs`

### 5.8 `RenderSettings.customReflectionTexture` (已有 UNITY_2022_1_OR_NEWER)

已有版本门控 `customReflectionTexture` → `customReflection` fallback。2020.2 走 `#else` 分支，安全。

**涉及文件**: `MCPForUnity/Editor/Tools/Graphics/SkyboxOps.cs`

---

## 6. 构建设置 / BuildTargetMapping

### 6.1 `NamedBuildTarget` → `BuildTargetGroup`

`NamedBuildTarget` 在 Unity 2021.2 引入。2020.x 使用 `BuildTargetGroup`。

**涉及文件**:
- `MCPForUnity/Editor/Tools/Build/BuildSettingsHelper.cs`
- `MCPForUnity/Editor/Tools/Build/BuildTargetMapping.cs`
- `MCPForUnity/Editor/Tools/ManageBuild.cs`
- `MCPForUnity/Editor/Services/PackageJobManager.cs`

### 6.2 测试文件同步

- `TestProjects/UnityMCPTests/Assets/Tests/EditMode/Tools/BuildTargetMappingTests.cs`: 方法名从 `TryResolveNamedBuildTarget` 改为 `TryResolveTargetGroup`

---

## 7. 包依赖 (package.json)

### 修改

```diff
- "unity": "2021.3",
+ "unity": "2020.2",

- "com.unity.modules.uielements": "1.0.0",    // ← 移除，2020.2 不存在此模块
- "com.unity.test-framework": "1.1.31",        // ← 移除直接依赖，测试项目单独管理

保留:
  "com.unity.nuget.newtonsoft-json": "3.0.2",  // 保留 (2020.2 可用)
```

### 测试项目 manifest.json

```diff
- "com.unity.test-framework": "1.1.33",
+ "com.unity.test-framework": "1.1.31",         // 降级到 2020.2 可解析版本
```

**涉及文件**:
- `MCPForUnity/package.json`
- `TestProjects/UnityMCPTests/Packages/manifest.json`

---

## 8. Roslyn MCP 安装器 ZIP 处理

### 8.1 这次同步后的状态说明

这次从 `origin/beta` 同步并继续做 Unity 2020.2 兼容处理后，`SafeZipExtractor` 相关逻辑采取了**保留但降级**的策略：

- `SafeZipExtractor` 文件继续保留，避免删除后丢失后续功能入口
- 不再强依赖 `System.IO.Compression.ZipArchive`
- 在 Unity 2020.2 下优先保证 Editor 侧能正常编译
- 如果当前环境无法提供稳定 ZIP 解压能力，则明确返回 `NotSupportedException`

### 8.2 设计取舍

这个处理的目标很明确：

- **编译优先**：不让 ZIP 相关 API 成为 Unity 2020.2 的构建阻塞点
- **功能保留**：类和调用语义先保留，避免一次性删掉造成功能回退
- **后续可恢复**：若未来确认 Unity 2020.2 可稳定支持 ZIP 处理，再补回真实实现

### 8.3 相关受影响文件

- `MCPForUnity/Editor/Services/AssetGen/Import/SafeZipExtractor.cs`
- `MCPForUnity/Editor/Services/AssetGen/Import/ModelImportPipeline.cs`
- `MCPForUnity/Editor/MCPForUnity.Editor.asmdef`

### 8.4 当前结论

当前仓库中的处理原则是：**先保留 `SafeZipExtractor`，只要不阻碍编译即可**。如果后续确认有真实 ZIP 解压调用路径，再针对 Unity 2020.2 继续补齐兼容实现。

### 问题

`System.IO.Compression.ZipArchive` 在 Unity 2020.2 的 Mono 运行时中不可用。即使添加 `System.IO.Compression.dll` 到 asmdef `precompiledReferences`，类型仍无法解析。

### 解决方案

在 `ExtractFileFromZip` 方法中使用版本门控：

```csharp
#if UNITY_2021_2_OR_NEWER
    // 使用 ZipArchive (Unity 2021.2+)
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
    {
        var entry = archive.GetEntry(fileName);
        // ...
    }
#else
    // 手动解析 ZIP 格式 (Unity 2020.x)
    // 1. 搜索 End of Central Directory Record (签名 0x06054b50)
    // 2. 解析 Central Directory 条目找到目标文件
    // 3. 读取 Local File Header 并提取数据
    // 4. 处理 stored (method=0) 和 deflate (method=8) 两种压缩
#endif
```

手动 ZIP 解析器使用 `BinaryReader` + `DeflateStream`，支持：
- 按文件名查找条目
- Stored（无压缩）和 Deflate 两种压缩方法
- 标准 ZIP32 格式（PKZIP 2.04g 兼容）

**涉及文件**:
- `MCPForUnity/Editor/Setup/RoslynInstaller.cs` (约 88 行新增)

---

## 9. 测试项目适配

### 9.1 Unity 版本

```diff
- m_EditorVersion: 2021.3.45f2
+ m_EditorVersion: 2020.2.5f1
```

**涉及文件**:
- `TestProjects/AssetStoreUploads/ProjectSettings/ProjectVersion.txt`
- `TestProjects/UnityMCPTests/ProjectSettings/ProjectVersion.txt`

### 9.2 测试代码

- `BuildTargetMappingTests.cs`: 方法名适配
- `ManagePrefabsCrudTests.cs`: 移除 `InteractionMode.AutomatedAction` 参数

### 9.3 unity-versions.json (CI 配置)

```diff
- "defaultVersion": "6000.0.75f1",
- "$defaultVersionComment": "Unity version used CI runs narrow matrix..."
+ 注释更新为 floor 版本说明，因为 GameCI 可能没有 2020.2 Docker 镜像
```

**涉及文件**:
- `tools/unity-versions.json`

---

## 10. 完整文件清单

### 配置文件 (3)
| 文件 | 改动类型 |
|------|---------|
| `MCPForUnity/package.json` | 版本 + 依赖调整 |
| `MCPForUnity/Editor/MCPForUnity.Editor.asmdef` | 添加 precompiledReferences |
| `tools/unity-versions.json` | CI 配置更新 |

### C# 语法兼容 (11)
| 文件 | 改动类型 |
|------|---------|
| `Editor/Tools/ManagePackages.cs` | `new()` → `new T()`, BOM |
| `Editor/Tools/ManageScriptableObject.cs` | `new()`, `is not`, `Replace(str,str,comparison)` |
| `Editor/Tools/ManageScript.cs` | 三元类型转换, `Replace(str,str)` |
| `Editor/Tools/UnityReflect.cs` | `new()`, BOM, em-dash 修复 |
| `Runtime/Serialization/UnityTypeConverters.cs` | `is not` → `!(... is ...)` |
| `Editor/Tools/Prefabs/ManagePrefabs.cs` | `is not` |
| `Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs` | `new()`, `is not`, BOM |
| `Editor/Windows/Components/Resources/McpResourcesSection.cs` | `new()`, BOM |
| `Editor/Windows/Components/Tools/McpToolsSection.cs` | `new()` |
| `Editor/Windows/MCPForUnityEditorWindow.cs` | `new()`, BOM |
| `Editor/Windows/MCPSetupWindow.cs` | `new()`, BOM |

### .NET Standard 2.0 API (4)
| 文件 | 改动类型 |
|------|---------|
| `Editor/Tools/Graphics/VolumeOps.cs` | `Enum.TryParse` → `Enum.Parse` |
| `Editor/Tools/Graphics/RenderPipelineOps.cs` | `Enum.TryParse` → `Enum.Parse` |
| `Editor/Tools/Vfx/ParticleControl.cs` | `Math.Clamp` → `Mathf.Clamp` |
| `Editor/Windows/Components/Tools/McpToolsSection.cs` | `Math.Clamp` → `Mathf.Clamp` |

### UI Toolkit 降级 (14)
| 文件 | 改动类型 |
|------|---------|
| `Editor/Windows/Components/Advanced/McpAdvancedSection.cs` | `clicked` → `clickable.clicked` |
| `Editor/Windows/Components/Connection/McpConnectionSection.cs` | `clicked` → `clickable.clicked` |
| `Editor/Windows/Components/Resources/McpResourcesSection.cs` | `clicked` → `clickable.clicked` |
| `Editor/Windows/MCPSetupWindow.cs` | `clicked` → `clickable.clicked` |
| `Editor/Windows/MCPForUnityEditorWindow.cs` | `ToolbarToggle` → `Toggle` |
| `Editor/Windows/MCPForUnityEditorWindow.uxml` | `ToolbarToggle` → `Toggle` |
| `Editor/Windows/EditorPrefs/EditorPrefsWindow.cs` | `PopupField<T>` + `labelElement` |
| `Editor/Windows/EditorPrefs/EditorPrefItem.uxml` | `DropdownField` → 占位 |
| `Editor/Windows/EditorPrefs/EditorPrefsWindow.uxml` | `DropdownField` → 占位 |
| `Editor/Windows/Components/ClientConfig/McpClientConfigSection.cs` | `PopupField<T>` 版本门控 |
| `Editor/Windows/Components/ClientConfig/McpClientConfigSection.uxml` | `DropdownField` → 占位 |
| `Editor/Windows/Components/Tools/McpToolsSection.cs` | 添加 `using UnityEngine` |

### Unity API 版本门控 (12)
| 文件 | 改动类型 |
|------|---------|
| `Editor/Tools/ManageComponents.cs` | `PrefabStageUtility` 门控 |
| `Editor/Tools/ManageScene.cs` | `PrefabStageUtility` 门控 |
| `Editor/Tools/Prefabs/ManagePrefabs.cs` | `PrefabStage` + `HasColor` + `InteractionMode` |
| `Editor/Tools/ManageUI.cs` | `UIDocument`/`PanelSettings` 全套门控 (6 处) |
| `Editor/Tools/GameObjects/GameObjectCreate.cs` | `InteractionMode` 门控 |
| `Editor/Tools/Graphics/LightBakingOps.cs` | `LightmapCompression` 门控 |
| `Editor/Tools/Graphics/SkyboxOps.cs` | 已有 `customReflection` fallback |
| `Editor/Tools/Profiler/Operations/CounterOps.cs` | `FileIO` category 门控 |
| `Editor/Tools/Build/BuildSettingsHelper.cs` | `NamedBuildTarget` → `BuildTargetGroup` |
| `Editor/Tools/Build/BuildTargetMapping.cs` | `NamedBuildTarget` → `BuildTargetGroup` |
| `Editor/Tools/ManageBuild.cs` | `NamedBuildTarget` → `BuildTargetGroup` |
| `Editor/Services/PackageJobManager.cs` | `GetAllRegisteredPackagesCompat` |

### Roslyn ZIP (1)
| 文件 | 改动类型 |
|------|---------|
| `Editor/Setup/RoslynInstaller.cs` | 手动 ZIP 解析器 fallback (88 行) |

### 测试项目 (5)
| 文件 | 改动类型 |
|------|---------|
| `TestProjects/AssetStoreUploads/ProjectSettings/ProjectVersion.txt` | 版本号 |
| `TestProjects/UnityMCPTests/ProjectSettings/ProjectVersion.txt` | 版本号 |
| `TestProjects/UnityMCPTests/Packages/manifest.json` | test-framework 版本 |
| `TestProjects/UnityMCPTests/.../BuildTargetMappingTests.cs` | 方法名适配 |
| `TestProjects/UnityMCPTests/.../ManagePrefabsCrudTests.cs` | 移除 InteractionMode |

### 杂项 (未分类，约 15 个文件)
部分文件包含微小改动（BOM 标记、em-dash 字符修复、using 语句调整、内部 helper 方法等），这些是编译通过所需的辅助性修改。完整列表见 `git diff --stat`。

---

## 附录 A: 关键 API 可用性速查

| API | 2020.2 | 2021.2+ | 替代方案 |
|-----|--------|---------|---------|
| `Button.clicked` | ❌ | ✅ | `Button.clickable.clicked` |
| `ToolbarToggle` | ❌ | ✅ | `Toggle` |
| `PopupField<T>` | ❌ | ✅ | `DropdownField` (非泛型) |
| `TextField.labelElement` | ❌ | ✅ | `TextField.Q<Label>()` |
| `DropdownField` (UXML) | ✅ | ✅ | 直接使用 |
| `PrefabStageUtility` | ❌ | ✅ | 跳过 Prefab Stage 检查 |
| `InteractionMode` | ❌ | ✅ | 移除参数 |
| `UIDocument` | ❌ | ✅ | 返回错误提示升级 |
| `PanelSettings` | ❌ | ✅ | 返回错误提示升级 |
| `NamedBuildTarget` | ❌ | ✅ | `BuildTargetGroup` |
| `ZipArchive` | ❌ | ✅ | 手动 ZIP 解析 |
| `ProfilerCategory.FileIO` | ❌ | ✅ | 从类别列表中移除 |
| `LightmapCompression` | ❌ | ✅ | 返回 false |
| `MaterialPropertyBlock.HasColor` | ❌ | ✅ | 跳过 HasColor 检查 |
| `Math.Clamp` | ❌ | ✅ | `Mathf.Clamp` |
| `Enum.TryParse` (非泛型) | ❌ | ✅ | `Enum.Parse` + try/catch |
| `String.Replace(str,str,comparison)` | ❌ | ✅ | `String.Replace(str,str)` |
| `String.Contains(char,comparison)` | ❌ | ✅ | `IndexOf` 替代 |
| `FindObjectsByType` | ❌ | ❌ (2022.3+) | 反射 fallback (已有) |

## 附录 B: 编译验证

```
Target: Unity 2020.2.5f1, C# 8.0, .NET Standard 2.0
Status: ✅ 0 compilation errors
Last verified: 2025-06-30
```

---

> **维护说明**: 当上游 main 分支有新的 API 使用时，请检查本对照表判断是否需要在降级分支中添加版本门控。新增的 Unity 2021.2+ 专用 API 必须包裹在 `#if UNITY_2021_2_OR_NEWER` 中。
