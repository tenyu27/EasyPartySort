using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace EasyPartySort;

[Serializable]
public class PartyOrderPreset
{
    public string Name { get; set; } = "";
    public List<string> PlayerNames { get; set; } = new();
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    public List<PartyOrderPreset> Presets { get; set; } = new();

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
