using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.Reflection;
using EasyPartySort.Windows;

namespace EasyPartySort.UI;

/// <summary>
/// Main UI content drawn inside EzConfigGui (ECommons). Holds state and draws party list + preset panel.
/// </summary>
public static class MainUI
{
    private const string PartyMemberPayloadType = "EPS_PartyMember";
    private const char KeySep = '\x01';
    private const float TileMinHeight = 40f;
    private static readonly Vector2 IconSize = new(24, 24);
    private const float MaxTileWidth = 500f;

    /// <summary>When > 0, refetch after this many frames.</summary>
    private static int s_refetchInFrames;
    private static readonly byte[] s_payloadBytes = new byte[256];

    public static void RefetchAfterFrames(int frames)
    {
        s_refetchInFrames = frames;
    }

    private static void RefetchPartyList()
    {
        Plugin.P!.Snapshot = PartyListHelper.GetPartyListInDisplayOrder(Svc.Data);
    }

    private static string GetMemberKey(PartyListHelper.PartyMemberEntry m)
        => $"{m.Name}{KeySep}{m.JobAbbr}{KeySep}{m.Level}";

    private static void MovePartyMemberByKey(List<PartyListHelper.PartyMemberEntry> list, string key, int newIndex)
    {
        int oldIndex = -1;
        for (int j = 0; j < list.Count; j++)
        {
            if (GetMemberKey(list[j]) == key)
            {
                oldIndex = j;
                break;
            }
        }
        if (oldIndex == -1 || newIndex < 0 || newIndex > list.Count) return;
        if (oldIndex == newIndex) return;
        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        newIndex = Math.Clamp(newIndex, 0, list.Count);
        list.Insert(newIndex, item);
    }

    public static void Draw()
    {
        if (Plugin.P == null) return;

        if (s_refetchInFrames > 0 && --s_refetchInFrames == 0)
            RefetchPartyList();

        if (ImGui.Button("Apply"))
        {
            var snapshot = Plugin.P.Snapshot;
            if (snapshot != null && snapshot.Count > 0)
            {
                PartyListHelper.ApplyPartyOrder(snapshot);
                s_refetchInFrames = 3;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Retrieve party list"))
            RefetchPartyList();

        ImGui.SameLine();
        if (ImGui.Button("Save as preset"))
        {
            var snapshot = Plugin.P.Snapshot;
            if (snapshot != null && snapshot.Count > 0)
                Plugin.P.PresetEditWindow.OpenForNew(snapshot.Select(m => m.Name).ToList());
        }

        ImGui.Separator();

        float leftW = 350f;
        using (var left = ImRaii.Child("Left", new Vector2(leftW, -1), true, ImGuiWindowFlags.None))
        {
            if (left.Success)
                DrawPartyList();
        }

        ImGui.SameLine();
        using (var right = ImRaii.Child("Right", Vector2.Zero, true, ImGuiWindowFlags.None))
        {
            if (right.Success)
                DrawPresetPanel();
        }
    }

    private static unsafe void DrawPartyList()
    {
        ImGui.Text("Drag to reorder");
        ImGui.Separator();

        var source = Plugin.P!.Snapshot;
        if (source == null || source.Count == 0)
        {
            RefetchPartyList();
            source = Plugin.P.Snapshot;
        }

        if (source == null || source.Count == 0)
        {
            ImGui.Text("No party list (solo or not in party).");
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            var m = source[i];
            ImGui.PushID(i);

            using (ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(8, 10)))
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4)))
            {
                var iconLookup = new GameIconLookup { IconId = m.IconId };
                var iconTexture = Svc.Texture.GetFromGameIcon(iconLookup).GetWrapOrDefault();
                if (iconTexture != null)
                {
                    ImGui.Image(iconTexture.Handle, IconSize);
                    ImGui.SameLine();
                }

                bool selected = false;
                float availX = ImGui.GetContentRegionAvail().X;
                float tileW = Math.Min(availX > 0 ? availX : 400f, MaxTileWidth);
                ImGui.Selectable(m.Name, selected, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(tileW, TileMinHeight));

                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    string key = GetMemberKey(m);
                    int len = Encoding.UTF8.GetBytes(key, 0, key.Length, s_payloadBytes, 0);
                    ImGui.SetDragDropPayload(PartyMemberPayloadType, new ReadOnlySpan<byte>(s_payloadBytes, 0, len), ImGuiCond.None);
                    ImGui.TextUnformatted(m.Name);
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload(PartyMemberPayloadType, ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                    try
                    {
                        IntPtr dataPtr = (IntPtr)payload.Data;
                        if (dataPtr != IntPtr.Zero && payload.DataSize > 0 && payload.DataSize <= s_payloadBytes.Length)
                        {
                            Marshal.Copy(dataPtr, s_payloadBytes, 0, (int)payload.DataSize);
                            string key = Encoding.UTF8.GetString(s_payloadBytes, 0, (int)payload.DataSize);
                            MovePartyMemberByKey(source, key, i);
                        }
                    }
                    catch (Exception) { }
                    ImGui.EndDragDropTarget();
                }
            }

            ImGui.PopID();
        }
    }

    private static void DrawPresetPanel()
    {
        ImGui.Text("Presets");
        ImGui.Separator();

        if (Plugin.P!.ShowLoadError && !string.IsNullOrEmpty(Plugin.P.LoadError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.3f, 0.3f, 1));
            ImGui.TextWrapped(Plugin.P.LoadError);
            ImGui.PopStyleColor();
            if (ImGui.Button("OK"))
            {
                Plugin.P.ShowLoadError = false;
                Plugin.P.LoadError = "";
            }
            ImGui.Separator();
        }

        var config = EzConfig.Get<Configuration>();
        var presets = config.Presets;
        if (presets.Count == 0)
        {
            ImGui.TextDisabled("No saved presets.");
            return;
        }

        for (int i = 0; i < presets.Count; i++)
        {
            var p = presets[i];
            ImGui.PushID(i);

            ImGui.Text(p.Name);
            ImGui.SameLine();
            ImGui.TextDisabled($"({p.PlayerNames.Count} players)");
            ImGui.Dummy(new Vector2(0, 2));

            if (ImGui.Button("Load"))
            {
                var (ok, err) = TryLoadPreset(p);
                if (!ok)
                {
                    Plugin.P.LoadError = err ?? "Failed to load.";
                    Plugin.P.ShowLoadError = true;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Edit"))
                Plugin.P.PresetEditWindow.OpenForPreset(p);
            ImGui.SameLine();
            bool ctrlHeld = ImGui.GetIO().KeyCtrl;
            ImGui.BeginDisabled(!ctrlHeld);
            if (ImGui.Button("Delete"))
            {
                presets.RemoveAt(i);
                EzConfig.Save();
                ImGui.EndDisabled();
                ImGui.PopID();
                ImGui.Separator();
                break;
            }
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && !ctrlHeld)
            {
                ImGui.BeginTooltip();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
                ImGui.TextUnformatted("Hold ctrl to delete");
                ImGui.PopStyleColor();
                ImGui.EndTooltip();
            }

            ImGui.PopID();
            ImGui.Separator();
        }
    }

    private static (bool success, string? error) TryLoadPreset(PartyOrderPreset preset)
    {
        var current = PartyListHelper.GetPartyListInDisplayOrder(Svc.Data);
        if (current == null || current.Count == 0)
            return (false, "No party list (solo or not in party).");
        if (current.Count != preset.PlayerNames.Count)
            return (false, $"Preset has {preset.PlayerNames.Count} players but party has {current.Count}.");

        var currentNames = new HashSet<string>(current.Select(m => m.Name));
        var presetNames = new HashSet<string>(preset.PlayerNames);
        if (!currentNames.SetEquals(presetNames))
        {
            var missing = presetNames.Except(currentNames).ToList();
            var extra = currentNames.Except(presetNames).ToList();
            var parts = new List<string>();
            if (missing.Count > 0)
                parts.Add("Missing in party: " + string.Join(", ", missing));
            if (extra.Count > 0)
                parts.Add("Not in preset: " + string.Join(", ", extra));
            return (false, string.Join(". ", parts));
        }

        var ordered = new List<PartyListHelper.PartyMemberEntry>();
        foreach (var name in preset.PlayerNames)
        {
            var entry = current.FirstOrDefault(m => m.Name == name);
            if (entry == null)
                return (false, "Name match failed.");
            ordered.Add(entry);
        }

        Plugin.P!.Snapshot = ordered;
        return (true, null);
    }
}
