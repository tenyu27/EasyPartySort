using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Logging;
using ECommons.Schedulers;
using ECommons.SimpleGui;
using EasyPartySort.UI;
using EasyPartySort.Windows;

namespace EasyPartySort;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/eps";

    public static Plugin? P { get; private set; }

    public List<PartyListHelper.PartyMemberEntry>? Snapshot { get; set; }
    public string LoadError { get; set; } = "";
    public bool ShowLoadError { get; set; }

    internal PresetEditWindow PresetEditWindow { get; private set; } = null!;

    public Plugin(IDalamudPluginInterface pi)
    {
        P = this;
        ECommonsMain.Init(pi, this, ECommons.Module.DalamudReflector);

        _ = new TickScheduler(() =>
        {
            EzConfig.Migrate<Configuration>();
            EzConfig.Init<Configuration>();

            PresetEditWindow = new PresetEditWindow();

            EzConfigGui.Init(DrawMain, EzConfig.Get<Configuration>(), null, EzConfigGui.WindowType.Both);

            if (EzConfigGui.Window != null)
            {
                EzConfigGui.Window.RespectCloseHotkey = true;
                EzConfigGui.Window.SetSizeConstraints(new Vector2(800, 700), new Vector2(float.MaxValue, float.MaxValue));
            }

            Svc.PluginInterface.UiBuilder.Draw += DrawPresetEditWindow;

            EzCmd.Add(CommandName, OnCommand, "Opens Easy Party Sort window.");

            DuoLog.Information($"EasyPartySort loaded. Use {CommandName} to open the window.");
        });
    }

    public void Dispose()
    {
        ECommonsMain.Dispose();
        Svc.PluginInterface.UiBuilder.Draw -= DrawPresetEditWindow;
        P = null;
    }

    private static void DrawMain()
    {
        if (EzConfigGui.Window != null && P != null)
        {
            var version = typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "?";
            var pluginName = ECommons.Reflection.DalamudReflector.GetPluginName() ?? "Easy Party Sort";
            EzConfigGui.Window.WindowName = $"{pluginName} v{version}###EasyPartySort";
        }
        MainUI.Draw();
    }

    private static void DrawPresetEditWindow()
    {
        if (P?.PresetEditWindow != null)
            P.PresetEditWindow.Draw();
    }

    private static void OnCommand(string command, string args)
    {
        EzConfigGui.Toggle();
    }
}
