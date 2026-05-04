# Easy Party Sort

Easily sort your party list in any order. Reorder members by dragging in the plugin window, save layouts as named presets, and switch between them anytime. Use `/eps` in chat to open the window.

## Build from source (standalone)

This repo is self-contained: all code lives under the EasyPartySort root. ECommons and OtterGui are **git submodules** in this repo (no references to other plugin folders).

**First-time setup — add submodules at repo root:**
```bash
cd EasyPartySort   # your clone root
git submodule add https://github.com/NightmareXIV/ECommons.git ECommons
git submodule add https://github.com/Ottermandias/OtterGui.git OtterGui
```

**If you cloned without submodules:**
```bash
git submodule update --init --recursive
```

Then build `EasyPartySort\EasyPartySort.csproj` (e.g. from the repo root) with the Dalamud SDK available.

## Install (custom repo)

1. In-game: `/xlsettings` -> **Experimental** -> **Custom Plugin Repositories**
2. Add (use the **raw** URL, not the github.com blob link):  
   `https://raw.githubusercontent.com/tenyu27/EasyPartySort/master/pluginmaster.json`
3. `/xlplugins` -> refresh -> install **Easy Party Sort** -> enable
4. `/eps` to open the plugin
