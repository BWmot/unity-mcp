<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="docs/images/logo-header-dark.png">
    <img alt="MCP for Unity" src="docs/images/logo-header-light.png" width="400">
  </picture>
</p>

<div align="center">

[English](README.md) <img src="docs/images/connector.svg" alt="↔" height="14"> [简体中文](docs/i18n/README-zh.md) &nbsp;&nbsp;&nbsp;|&nbsp;&nbsp;&nbsp; [Discord](https://discord.gg/y4p8KfzrN4) <img src="docs/images/connector.svg" alt="↔" height="14"> [Wiki](https://bwmot.github.io/unity-mcp/)

#### Proudly sponsored and maintained by [Aura](https://www.tryaura.dev/) — the AI assistant for Unreal & Unity.
##### And don't miss [Godot AI](https://github.com/hi-godot/godot-ai), the new open source project from the makers of MCP for Unity.

</div>

<p align="center"><b>Create your Unity apps with LLMs.</b> MCP for Unity bridges AI assistants — Claude, Codex, VS Code, local LLMs, and more — with your Unity Editor via <a href="https://modelcontextprotocol.io/introduction">Model Context Protocol</a>. Give your LLM the tools to manage assets, control scenes, edit scripts, run tests, and automate your game dev workflows.</p>

<p align="center">
  <img alt="MCP for Unity building a scene" src="docs/images/building_scene.gif">
</p>

---

> **⚠️ This is the `unity2020-mcp` branch** — a **Unity 2020.2-compatible** derivative of
> [MCP for Unity](https://github.com/CoplayDev/unity-mcp), continuously synced with upstream `beta`.
>
> - **Targets Unity 2020.2 → 6.x** (upstream requires 2021.3+). All C# / UI Toolkit code is
>   adapted to compile under Unity 2020.2 (C# 8.0 / .NET Standard 2.0) while staying
>   feature-identical to upstream.
> - **Package name:** `com.bwmol.unitymcp2020` (changed from `com.coplaydev.unity-mcp` to avoid
>   clashing with the upstream package).
> - **Install:** `https://github.com/BWmot/unity-mcp.git?path=/MCPForUnity#unity2020-mcp`
> - **Upstream:** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) — merge upstream
>   changes into this branch with `git merge upstream/beta`.

<!-- recent-updates:start -->
<details>
<summary><strong>Recent Updates</strong></summary>

* **v10.1.3-beta.4** (2026-08-19) — synced with upstream `beta` (v10.1.3-beta.4), incl. AssetGen
  audio backend, `UvInstaller`, OceanMark branding, stdio timeout config. Compiles on Unity 2020.2.
* **v10.0.1-beta.1** — initial fork baseline from upstream `beta`.

Upstream release history: [coplaydev.github.io/unity-mcp/releases](https://coplaydev.github.io/unity-mcp/releases).

</details>
<!-- recent-updates:end -->

---

## What it does

Control the Unity Editor in natural language from any MCP client — create scenes & GameObjects, edit C# scripts, manage assets, run tests, profile, and build. 47 focused MCP tool entrypoints, any client, free & MIT.

**[Browse the full tool catalog →](https://coplaydev.github.io/unity-mcp/reference/tools/)**

---

## Quickstart

**Requirements:** Unity **2020.2 → 6.x** (this branch lowers the floor from 2021.3 to 2020.2) · Python **3.10+** (via [`uv`](https://docs.astral.sh/uv/)). Works with **any MCP client** — Claude Desktop & Code, Cursor, VS Code, Windsurf, Cline, Gemini CLI, and more.

1. **Install** — Unity → Package Manager → Add from git URL:
  `https://github.com/BWmot/unity-mcp.git?path=/MCPForUnity#unity2020-mcp` &nbsp;_(always tracks the `unity2020-mcp` branch; or `openupm add com.bwmol.unitymcp2020`)_
2. **Configure** — `Window → MCP for Unity → Configure All Detected Clients`.
3. **Prompt** — *"Create a cube at the origin and add a Rigidbody."* The cube appears in seconds.

---

## Community

- [Discord](https://discord.gg/y4p8KfzrN4) — chat with maintainers and other contributors
- [Issues](https://github.com/BWmot/unity-mcp/issues) — bugs and feature requests
- [Discussions](https://github.com/BWmot/unity-mcp/discussions) — design ideas and broader questions
- Security: see [SECURITY.md](SECURITY.md) for private reporting

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Branch off `unity2020-mcp`, not `main`. The full dev setup, testing, and release process live in the [Contributing](https://bwmot.github.io/unity-mcp/contributing/dev-setup) docs.

## Advanced

- **Multiple Unity instances** — [Multi-Instance Routing](https://bwmot.github.io/unity-mcp/guides/multi-instance)
- **Tool groups (vfx / animation / ui / testing / etc.)** — [Tool Groups](https://bwmot.github.io/unity-mcp/guides/tool-groups)
- **v10 asset generation and upgrade notes** — [v10 Migration](https://bwmot.github.io/unity-mcp/migrations/v10)
- **Roslyn script validation** — [Roslyn Validation](https://bwmot.github.io/unity-mcp/guides/roslyn)
- **Remote-hosted server with auth** — [Remote Server Auth](https://bwmot.github.io/unity-mcp/guides/remote-server-auth)

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=BWmot/unity-mcp&type=Date)](https://www.star-history.com/#BWmot/unity-mcp&Date)

## Citation

If MCP for Unity helped your research, please cite it.

```bibtex
@inproceedings{wu2025mcpunity,
  author    = {Wu, Shutong and Barnett, Justin P.},
  title     = {{MCP-Unity}: {Protocol-Driven} Framework for Interactive {3D} Authoring},
  year      = {2025},
  isbn      = {9798400721366},
  publisher = {Association for Computing Machinery},
  address   = {New York, NY, USA},
  url       = {https://doi.org/10.1145/3757376.3771417},
  doi       = {10.1145/3757376.3771417},
  series    = {SA Technical Communications '25}
}
```

## Unity AI Tools by Aura

Aura offers 2 AI tools for Unity:
- **MCP for Unity** is available freely under the MIT license.
- **Aura for Unity** is a premium Unity/Unreal AI assistant built for game devs.

## Disclaimer

This project is a free and open-source tool for the Unity Editor, and is not affiliated with Unity Technologies.

---

**License:** MIT — see [LICENSE](LICENSE).
