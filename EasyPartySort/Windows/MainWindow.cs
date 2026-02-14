using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;

namespace EasyPartySort.Windows;

public class MainWindow : Window, IDisposable
{
    private const string PartyMemberPayloadType = "EPS_PartyMember";

    /// <summary>Stable key separator (same as PartySortPlus-style: identify item by key, not index).</summary>
    private const char KeySep = '\x01';

    /// <summary>Minimum height per tile to make them easier to drag.</summary>
    private const float TileMinHeight = 40f;

    /// <summary>Job icon size in tile.</summary>
    private static readonly Vector2 IconSize = new(24, 24);

    /// <summary>Max width of a tile so the list doesn't stretch too much on wide windows.</summary>
    private const float MaxTileWidth = 500f;

    private readonly Plugin plugin;
    private List<PartyListHelper.PartyMemberEntry>? _snapshot;

    /// <summary>When > 0, refetch after this many frames (lets the game apply the new order before we read).</summary>
    private int _refetchInFrames;

    /// <summary>Payload buffer for drag (stable key bytes). Reused to avoid alloc in loop.</summary>
    private readonly byte[] _payloadBytes = new byte[256];

    private readonly PresetEditWindow _presetEditWindow;
    private string _loadError = "";
    private bool _showLoadError;

    /// <summary>Minimum window size: enough for both panels and 8 party rows.</summary>
    private static readonly Vector2 MinWindowSize = new(800f, 700f);

    public MainWindow(Plugin plugin, PresetEditWindow presetEditWindow)
        : base("Easy Party Sort##EasyPartySortMain", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = MinWindowSize,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
        _presetEditWindow = presetEditWindow;
    }

    public void Dispose() { }

    private void RefetchPartyList()
    {
        _snapshot = PartyListHelper.GetPartyListInDisplayOrder(Plugin.DataManager);
    }

    /// <summary>Called when loading a preset from another window; refetch after a few frames.</summary>
    internal void RefetchAfterFrames(int frames)
    {
        _refetchInFrames = frames;
    }

    /// <summary>Builds a stable key for a party member (PartySortPlus-style: identify by key so reorder doesn't change identity).</summary>
    private static string GetMemberKey(PartyListHelper.PartyMemberEntry m)
    {
        return $"{m.Name}{KeySep}{m.JobAbbr}{KeySep}{m.Level}";
    }

    /// <summary>Moves the member matching the given key so it ends up at newIndex (0-based).</summary>
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
        if (oldIndex == -1 || newIndex < 0 || newIndex > list.Count)
            return;
        if (oldIndex == newIndex)
            return;
        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        // After removal, list has one fewer element. To place item at final index newIndex, insert at newIndex.
        // When oldIndex < newIndex, the slot we want is still at newIndex (e.g. [B,C,D], insert at 2 -> [B,C,A,D]).
        // When oldIndex > newIndex, insert at newIndex. No adjustment.
        newIndex = Math.Clamp(newIndex, 0, list.Count);
        list.Insert(newIndex, item);
    }

    public override unsafe void Draw()
    {
        if (_refetchInFrames > 0 && --_refetchInFrames == 0)
            RefetchPartyList();

        if (ImGui.Button("Apply"))
        {
            if (_snapshot != null && _snapshot.Count > 0)
            {
                PartyListHelper.ApplyPartyOrder(_snapshot);
                _refetchInFrames = 3; // Refetch after a few frames so the game has applied the new order
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Retrieve party list"))
        {
            RefetchPartyList();
        }

        ImGui.SameLine();
        if (ImGui.Button("Save as preset"))
        {
            if (_snapshot != null && _snapshot.Count > 0)
                _presetEditWindow.OpenForNew(_snapshot.Select(m => m.Name).ToList());
        }

        ImGui.Separator();

        float availX = ImGui.GetContentRegionAvail().X;
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

    private unsafe void DrawPartyList()
    {
        ImGui.Text("Drag to reorder");
        ImGui.Separator();

        var source = _snapshot;
        if (source == null || source.Count == 0)
        {
            RefetchPartyList();
            source = _snapshot;
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
                    // Draw icon then selectable (name) in normal layout flow — no SetCursorScreenPos
                    var iconLookup = new GameIconLookup { IconId = m.IconId };
                    var iconTexture = Plugin.TextureProvider.GetFromGameIcon(iconLookup).GetWrapOrDefault();
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
                        int len = Encoding.UTF8.GetBytes(key, 0, key.Length, _payloadBytes, 0);
                        ImGui.SetDragDropPayload(PartyMemberPayloadType, new ReadOnlySpan<byte>(_payloadBytes, 0, len), ImGuiCond.None);
                        ImGui.TextUnformatted(m.Name);
                        ImGui.EndDragDropSource();
                    }

                    if (ImGui.BeginDragDropTarget())
                    {
                        // Accept only on actual drop (no AcceptBeforeDelivery) so the list doesn't reorder during drag:
                        // preview stays visible and drop works in both directions.
                        var payload = ImGui.AcceptDragDropPayload(PartyMemberPayloadType, ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                        try
                        {
                            IntPtr dataPtr = (IntPtr)payload.Data;
                            if (dataPtr != IntPtr.Zero && payload.DataSize > 0 && payload.DataSize <= _payloadBytes.Length)
                            {
                                Marshal.Copy(dataPtr, _payloadBytes, 0, (int)payload.DataSize);
                                string key = Encoding.UTF8.GetString(_payloadBytes, 0, (int)payload.DataSize);
                                MovePartyMemberByKey(source, key, i);
                            }
                        }
                        catch (Exception)
                        {
                            // Payload invalid — ignore
                        }
                        ImGui.EndDragDropTarget();
                    }
                }

                ImGui.PopID();
            }
    }

    private void DrawPresetPanel()
    {
        ImGui.Text("Presets");
        ImGui.Separator();

        if (_showLoadError && !string.IsNullOrEmpty(_loadError))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 0.3f, 0.3f, 1));
            ImGui.TextWrapped(_loadError);
            ImGui.PopStyleColor();
            if (ImGui.Button("OK"))
            {
                _showLoadError = false;
                _loadError = "";
            }
            ImGui.Separator();
        }

        var presets = plugin.Configuration.Presets;
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
                    _loadError = err ?? "Failed to load.";
                    _showLoadError = true;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Edit"))
                _presetEditWindow.OpenForPreset(p);
            ImGui.SameLine();
            bool ctrlHeld = ImGui.GetIO().KeyCtrl;
            ImGui.BeginDisabled(!ctrlHeld);
            if (ImGui.Button("Delete"))
            {
                presets.RemoveAt(i);
                plugin.Configuration.Save();
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

    private (bool success, string? error) TryLoadPreset(PartyOrderPreset preset)
    {
        var current = PartyListHelper.GetPartyListInDisplayOrder(Plugin.DataManager);
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

        _snapshot = ordered;
        return (true, null);
    }
}
