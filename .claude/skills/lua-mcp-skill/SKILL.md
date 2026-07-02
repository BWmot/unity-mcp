---
name: lua-mcp-skill
description: Guide for using Lua execution and snippet management tools in the Unity MCP package. Use when testing, debugging, or reusing Lua code through lua_execute, lua_backend_status, and lua_snippet_manage.
---

# Lua MCP Skill

This skill helps you work with the Lua-related MCP tools in the Unity package.

## When to use

Use this skill when you need to:
- execute Lua code in Unity
- check which Lua backend is available or active
- manage reusable snippets and categories for persistent Lua scripts
- debug xLua Standalone or xLua InGame execution

## Available tools

### `lua_execute`
Execute a Lua snippet directly.

Use it for:
- quick experiments
- debugging runtime behavior
- validating code against the current Lua backend

Important:
- `xLua (InGame)` requires Unity Play Mode and a live game Lua environment
- if you only need editor-side execution, prefer `xLua (Standalone)` or the Code Executor route
- if a snippet needs to be reused later, promote it into a managed snippet

### `lua_backend_status`
Inspect the current Lua tool setup.

Use it before execution when you need to know:
- whether Lua tools are enabled
- whether Code Executor integration is available
- which backend is preferred or currently usable

### `lua_snippet_manage`
Manage persistent snippets and categories.

Use it for:
- creating reusable Lua scripts
- organizing scripts into categories
- renaming or moving snippets
- deleting old test snippets

This is the tool to use when you want repeatable, shareable Lua code rather than one-off execution.

## Recommended workflow

1. Check backend status with `lua_backend_status`
2. Choose the right execution mode
3. Run a small test with `lua_execute`
4. If the code should be reused, save it with `lua_snippet_manage`
5. Re-run from the managed snippet later if needed

## Execution mode guidance

- Use `xLua (Standalone)` for editor-side Lua tests
- Use `xLua (InGame)` only when the game is already in Play Mode
- Use Code Executor-backed modes when you want to reuse the project's existing execution pipeline

## Safety notes

- Do not assume InGame Lua is available outside Play Mode
- Do not delete a category unless you are sure its snippets are no longer needed
- Prefer creating a snippet first when you expect to run the same Lua code more than once

## Example use cases

- verify whether a runtime singleton exists
- test a `require(...)` path
- store a utility function for later reuse
- organize project-specific Lua helpers by category
