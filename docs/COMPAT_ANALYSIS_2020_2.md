# Unity MCP 兼容性分析：从 Unity 2021.3 降级到 Unity 2020.2.5f1

> **创建日期**: 2025-01  
> **目标版本**: Unity 2020.2.5f1  
> **原始最低版本**: Unity 2021.3  
> **涉及文件数**: 约 15-20 个 C# 文件 + 配置文件

---

## 总体评估

| 类别 | 问题数量 | 严重程度 |
|------|---------|---------|
| 配置文件 (package.json) | 2 | 🔴 阻断 |
| UI Toolkit API 变更 | ~70 处 (4-5种模式) | 🔴 阻断 |
| 构建 API (NamedBuildTarget) | 约 8 处 | 🟡 需条件编译 |
| PrefabUtility (InteractionMode) | 3 处 | 🟡 需条件编译 |
| FindObjectsByType (已处理) | 0 | ✅ 已有兼容层 |
| 其他 (UNITY_60, UNITY_2022) | 0 | ✅ 已有条件保护 |

---

## 1. 🔴 package.json — 阻断性问题

**文件**: `MCPForUnity/package.json`

### 1.1 Unity 版本字段
```json
"unity": "2021.3"   // 当前值
"unity": "2020.2"   // 需改为
```

### 1.2 移除不存在的模块依赖

**`com.unity.modules.uielements: 1.0.0`** — **必须移除**

| 版本 | UI Toolkit 位置 |
|------|----------------|
| Unity 2020.2 | `UnityEngine.UIElements` / `UnityEditor.UIElements` 在 Editor 核心程序集中（无需包依赖） |
| Unity 2021.1 | `com.unity.ui` 包（实验阶段） |
| Unity 2021.3+ | `com.unity.modules.uielements` 内置模块 |

> 在 2020.2 中，所有 UI Toolkit 基础类型（VisualElement、Button、Toggle、TextField、Foldout 等）
> 都在 Editor 核心程序集中自动可用，无需声明包依赖。
> 声明 `com.unity.modules.uielements` 会导致包解析失败——该模块在 2020.2 中不存在。

**结论**: 从 `dependencies` 中删除 `"com.unity.modules.uielements": "1.0.0"`。

### 1.3 manifest.json
项目级别的 `manifest.json` 无版本限制字段，无需修改。

---

## 2. 🔴 UI Toolkit API 变更 — 最大的兼容性问题

### 2.1 `Button.clicked` 事件 (39 处，7 个文件)

**问题**: `Button.clicked` 是 Unity 2021.1 新增的语法糖。在 2020.2 中必须使用 `button.clickable.clicked`。

```csharp
// ❌ 2021.1+ (当前代码)
refreshButton.clicked += OnRefreshClicked;
doneButton.clicked    += () => { ... };

// ✅ 2020.2 兼容
refreshButton.clickable.clicked += OnRefreshClicked;
doneButton.clickable.clicked    += () => { ... };
```

> **注意**: Clickable 对象在 `Button` 构造函数中创建，但 `button.clickable` 在构造期间
> 可能为 null（取决于 Unity 内部实现）。需要确保在 `clickable` 不为 null 时注册。

**涉及文件** (共 39 处):
| 文件 | 约计数 |
|------|--------|
| `McpAdvancedSection.cs` | ~11 |
| `McpClientConfigSection.cs` | ~9 |
| `McpConnectionSection.cs` | ~6 |
| `McpResourcesSection.cs` | ~3 |
| `McpToolsSection.cs` | ~4 |
| `EditorPrefsWindow.cs` | ~1 |
| `MCPSetupWindow.cs` | ~5 |

**建议方案**: 统一替换 `.clicked +=` → `.clickable.clicked +=`。

或在 `MCPForUnity.Editor.asmdef` 中添加 `UNITY_2021_1_OR_NEWER` 条件编译：
```csharp
#if UNITY_2021_1_OR_NEWER
    button.clicked += handler;
#else
    button.clickable.clicked += handler;
#endif
```

但推荐直接使用 `clickable.clicked`，因为它在所有版本中都可用，无需条件编译。

### 2.2 `ToolbarToggle` (5 处，2 个文件)

**问题**: `ToolbarToggle` 在 Unity 2021.2 才引入。2020.2 中不存在。

**文件**:
- `MCPForUnityEditorWindow.cs` (C# 字段声明 + 查询)
- `MCPForUnityEditorWindow.uxml` (UXML 模板)

**替换方案**:
- C#: 将 `ToolbarToggle` 替换为 `Toggle`，手动设置 Toolbar 样式
- UXML: 将 `<uie:ToolbarToggle>` 替换为 `<uie:Toggle>`

```csharp
// ❌ 2021.2+ (当前代码)
private ToolbarToggle tabGeneral;
private ToolbarToggle tabTools;
// ...
tabGeneral = rootVisualElement.Q<ToolbarToggle>("tab-general");

// ✅ 2020.2 兼容
private Toggle tabGeneral;
private Toggle tabTools;
// ...
tabGeneral = rootVisualElement.Q<Toggle>("tab-general");
```

**UXML 修改** (第 10-16 行):
```xml
<!-- ❌ 原代码 -->
<uie:Toolbar>
    <uie:ToolbarToggle name="tab-general" text="General" />
    <uie:ToolbarToggle name="tab-tools" text="Tools" />
    ...
</uie:Toolbar>

<!-- ✅ 替换 -->
<uie:Toolbar>
    <uie:Toggle name="tab-general" text="General" />
    <uie:Toggle name="tab-tools" text="Tools" />
    ...
</uie:Toolbar>
```

### 2.3 `TextField.labelElement` (1 处)

**问题**: `TextField.labelElement` 在 2021.1 引入。

**文件**: `EditorPrefsWindow.cs` (约第 139 行)

```csharp
// ❌ 当前代码
searchField.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;

// ✅ 2020.2 兼容
// 方案 1: 使用 Q<Label>
searchField.Q<Label>().style.unityFontStyleAndWeight = FontStyle.Bold;

// 方案 2: 使用条件编译
#if UNITY_2021_1_OR_NEWER
searchField.labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
#else
searchField.Q<Label>().style.unityFontStyleAndWeight = FontStyle.Bold;
#endif
```

### 2.4 `DropdownField` (3 处)

**问题**: `DropdownField` 在 2021.1 才引入。2020.2 中使用 `PopupField<string>`。

**文件**: 
- `EditorPrefsWindow.cs` (Line ~329: `itemElement.Q<DropdownField>("type-dropdown")`)
- 可能的其他文件

```csharp
// ❌ 2021.1+
var typeDropdown = itemElement.Q<DropdownField>("type-dropdown");

// ✅ 2020.2
var typeDropdown = itemElement.Q<PopupField<string>>("type-dropdown");
```

**UXML 修改**: 将 `<uie:DropdownField>` 替换为 `<uie:PopupField>` 并设置 `choices` 属性。

### 2.5 `EnumField` (2 处，2 个文件)

**状态**: ✅ `EnumField` 在 Unity 2019.3 中已可用，2020.2 中安全使用。

**文件**:
- `McpConnectionSection.cs`
- `McpValidationSection.cs`

`EnumField` 无需修改。

### 2.6 `RegisterValueChangedCallback<T>` (27 处)

**状态**: ✅ 此 API 自 2019.3 可用，2020.2 中安全使用。

`RegisterValueChangedCallback` 无需修改。

---

## 3. 🟡 构建 API：`NamedBuildTarget` (约 8 处，4 个文件)

**问题**: `NamedBuildTarget` 在 Unity 2021.2 才引入。2020.2 中直接使用 `BuildTargetGroup`。

**涉及文件**:
| 文件 | 使用 |
|------|------|
| `Build/BTargetMapping.cs` | `NamedBuildTarget.FromBuildTargetGroup()` |
| `Build/BSettingsHelper.cs` | 参数类型 `NamedBuildTarget` |
| `ManageBuild.cs` | 构建管理 |
| 其他构建相关文件 | |

**修复方案**:
```csharp
// 在 BTargetMapping.cs 中
#if UNITY_2021_2_OR_NEWER
    var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
#else
    // 2020.2: 直接使用 BuildTargetGroup
    // PlayerSettings.GetScriptingDefineSymbols(group, out defines)
#endif
```

需要系统性地检查所有 `NamedBuildTarget` 使用，并用 `BuildTargetGroup` 替代。

---

## 4. 🟡 PrefabUtility：`InteractionMode` 枚举 (3 处，2 个文件)

**问题**: `InteractionMode` 枚举在 Unity 2021.2 引入。

**文件**:
- `GameObjects/GameObjectCreate.cs` (Line 298)
- `Prefabs/ManagePrefabs.cs` (Lines 135, 272)

```csharp
// ❌ 2021.2+
PrefabUtility.SaveAsPrefabAssetAndConnect(newGo, finalPrefabPath, InteractionMode.UserAction);
PrefabUtility.UnpackPrefabInstance(rootToUnlink, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

// ✅ 2020.2
#if UNITY_2021_2_OR_NEWER
    PrefabUtility.SaveAsPrefabAssetAndConnect(newGo, finalPrefabPath, InteractionMode.UserAction);
#else
    PrefabUtility.SaveAsPrefabAssetAndConnect(newGo, finalPrefabPath);
#endif
```

---

## 5. ✅ 已有的兼容层（无需修改）

### 5.1 `UnityFindObjectsCompat` 
已有 `UNITY_2022_3_OR_NEWER` / `else` (反射 fallback) 条件编译。2020.2 走反射路径，安全。

### 5.2 `SkyboxOps.cs`
已有 `#if UNITY_2022_1_OR_NEWER` 保护 `RenderSettings.customReflectionTexture`，有旧版 `customReflection` fallback。

### 5.3 `ManageAsset.cs`
`#if UNITY_60_0OR_NEWER` 处理 `PhysicsMaterial` 重命名，2020.2 走 else 分支，安全。

### 5.4 Profiler 相关
`UNITY_2022_2_OR_NEWER` 保护 Profiler API，2020.2 自动跳过，安全。

### 5.5 `ManageScene.cs`
`#if !UNITY_2022_1_OR_NEWER` 有旧版 fallback。

### 5.6 `UnityWebRequest`
仅在 `RoslynInstaller.cs` 中使用，`UnityWebRequest` API 在 2020.2 中完全可用。

---

## 6. 🔧 其他需要注意的项

### 6.1 `tools/unity-versions.json`
CI 测试版本矩阵，需添加 2020.2 条目或调整最低版本：
```json
{
    "floor": "2020.2.5f1",
    ...
}
```

### 6.2 Newtonsoft.Json 3.0.2
`com.unity.nuget.newtonsoft-json: 3.0.2` 在 Unity 2020.2 的包注册表中可用。

### 6.3 `com.unity.test-framework: 1.1.31`
在 Unity 2020.2 中可用。

### 6.4 `com.unity.modules.imageconversion`
在 Unity 2020.2 中可用。

### 6.5 `com.unity.modules.screencapture`
在 Unity 2020.2 中 **可能** 需要确认可用性。`ScreenCapture` 类自 Unity 2018.1 可用，但模块化后的 `com.unity.modules.screencapture` 可能在后续版本才独立。如果不存在，需移除此依赖。

### 6.6 `Application.isBatchMode`
在 Unity 2020.2 中可用。

### 6.7 `CompilationPipeline` 回调
在 Unity 2020.2 中可用。

---

## 7. 📋 修改优先级与实施建议

### 第一阶段：配置文件（阻断性）
1. ✅ 修改 `package.json`: `"unity": "2020.2"`
2. ✅ 移除 `"com.unity.modules.uielements": "1.0.0"`
3. ✅ 验证 `"com.unity.modules.screencapture"` 在 2020.2 中的可用性

### 第二阶段：UI Toolkit API 批量替换（阻断性）
4. 批量替换 `.clicked +=` → `.clickable.clicked +=` (39处)
5. 替换 `ToolbarToggle` → `Toggle` (C# + UXML)
6. 替换 `TextField.labelElement` → `Q<Label>()` (1处)
7. 替换 `DropdownField` → `PopupField<string>` (C# + UXML)

### 第三阶段：条件编译保护（非阻断）
8. `NamedBuildTarget` → `BuildTargetGroup` (加 `UNITY_2021_2_OR_NEWER`)
9. `InteractionMode` 参数移除 (加 `UNITY_2021_2_OR_NEWER`)

### 第四阶段：CI/测试
10. 更新 `unity-versions.json`
11. 在实机 2020.2.5f1 上测试编译
12. 运行测试套件

---

## 8. ⚠️ 风险评估

| 风险 | 等级 | 说明 |
|------|------|------|
| `clickable.clicked` 时序 | 中 | Clickable 创建时机可能需要验证（在某些 Button 构造期间可能为 null） |
| UXML 模板兼容 | 中 | 替换 DropdownField、ToolbarToggle 后需要验证模板加载 |
| `com.unity.modules.screencapture` | 低 | 如不存在需移除，但 ScreenCapture 类在核心模块中依然可用 |
| Newtonsoft.Json 版本 | 低 | 3.0.2 应在 2020.2 注册表中可解析 |
| 运行时兼容 | 低 | 所有 Runtime 代码使用 compat shim，向下兼容已处理 |

---

## 9. 附录：关键 API 版本对照表

| API | 最低版本 | 2020.2 状态 |
|-----|---------|------------|
| `Button.clicked` | 2021.1 | ❌ 不可用 → 用 `clickable.clicked` |
| `ToolbarToggle` | 2021.2 | ❌ 不可用 → 用 `Toggle` |
| `TextField.labelElement` | 2021.1 | ❌ 不可用 → 用 `Q<Label>()` |
| `DropdownField` | 2021.1 | ❌ 不可用 → 用 `PopupField<string>` |
| `EnumField` | 2019.3 | ✅ 可用 |
| `NamedBuildTarget` | 2021.2 | ❌ 不可用 → 用 `BuildTargetGroup` |
| `InteractionMode` | 2021.2 | ❌ 不可用 → 移除参数 |
| `FindObjectsByType` | 2022.3 | ❌ 不可用 → 已有反射 fallback |
| `customReflectionTexture` | 2022.1 | ❌ → 已有 `customReflection` fallback |
| `Physics.simulationMode` | 2022.2 | ⚠️ → 已有 compat shim |
| `RegisterValueChangedCallback` | 2019.3 | ✅ 可用 |
| `Foldout` | 2019.3 | ✅ 可用 |
| `VisualElement` | 2019.3 | ✅ 可用 |
| `UnityWebRequest` | 2017.1 | ✅ 可用 |
| async/await | C# 7.0+ | ✅ 可用 |
