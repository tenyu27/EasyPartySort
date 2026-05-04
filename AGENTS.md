# AI Agent Context: Easy Party Sort

This file provides essential context, architecture details, and development guidelines for AI agents working on the **Easy Party Sort** repository.

## 1. Project Context
**Easy Party Sort** is a Final Fantasy XIV plugin built on the Dalamud framework. It enables users to sort their party list in any custom order by dragging and dropping members in the plugin window. Layouts can be saved as named presets and swapped dynamically.
- **In-Game Command:** `/eps` toggles the main UI.

## 2. Architecture & Dependencies
The plugin is written in C# and targets the Dalamud `.NET` runtime.

### Dependencies
- **Dalamud SDK:** `Dalamud.NET.Sdk` (v15.0.0+).
- **ECommons:** (Git Submodule) Provides utilities for UI, config, and logging.
- **OtterGui:** (Git Submodule) Provides advanced ImGui elements.
- **Lumina:** Reads FFXIV game data sheets (e.g., `ClassJob`).
- **FFXIVClientStructs:** Reverse-engineered FFXIV memory structures.

### Key Components
- `Plugin.cs`: Entry point (`IDalamudPlugin`), handles ECommons init and the `/eps` command.
- `Configuration.cs`: Handles savable plugin configurations (`IPluginConfiguration`) and presets.
- `PartyListHelper.cs`: 
  - **Read:** Reads `AgentHUD` from `FFXIVClientStructs` to get the game's actual party list UI order.
  - **Write:** Uses `InfoProxyPartyMember.ChangeOrder(currentIdx, newIdx)` to tell the game client to swap members.

## 3. Development Guidelines & References

When generating or modifying code for this project, refer to the following resources:

### Core References
- **Dalamud API Reference:** [https://dalamud.dev/api/](https://dalamud.dev/api/) (Check here for interface definitions and available services).
- **Goatcorp (Dalamud parent):** [https://github.com/goatcorp](https://github.com/goatcorp) | [Dalamud Repo](https://github.com/goatcorp/Dalamud/)
- **FFXIVClientStructs:** [https://github.com/aers/FFXIVClientStructs/](https://github.com/aers/FFXIVClientStructs/) (Check here when game patches alter memory offsets).

### Patch Update Protocol (Dalamud API Updates)
When a new FFXIV patch releases, Dalamud updates its framework, which may break plugins. Follow these steps when tasked with updating the plugin:
1. **Check Tags:** Look at [https://github.com/goatcorp/Dalamud/tags](https://github.com/goatcorp/Dalamud/tags) to identify the latest stable API version matching the game patch. Update the `.csproj` SDK version accordingly.
2. **Review Structs:** Verify that memory offsets (like `AgentHUD` and `InfoProxyPartyMember` in `PartyListHelper.cs`) align with the latest `FFXIVClientStructs` definitions.
3. **Adapt API Changes:** Use `dalamud.dev/api/` to resolve breaking changes in `IDalamudPlugin` services.
