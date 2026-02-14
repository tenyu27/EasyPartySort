using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace EasyPartySort.Windows;

public class PresetEditWindow : Window, IDisposable
{
    private const string PresetNamePayloadType = "EPS_PresetName";

    private readonly Plugin _plugin;
    private string _presetName = "";
    private List<string> _names = new();
    private PartyOrderPreset? _preset;
    private bool _isNewPreset;
    private readonly byte[] _payloadBytes = new byte[256];

    public PresetEditWindow(Plugin plugin)
        : base("Preset##EasyPartySortPresetEdit", ImGuiWindowFlags.None)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500)
        };
        _plugin = plugin;
    }

    public void Dispose() { }

    public void OpenForNew(List<string> playerNamesInOrder)
    {
        _preset = null;
        _isNewPreset = true;
        _presetName = "";
        _names = new List<string>(playerNamesInOrder);
        WindowName = "Save as preset##EasyPartySortPresetEdit";
        IsOpen = true;
    }

    public void OpenForPreset(PartyOrderPreset preset)
    {
        _preset = preset;
        _isNewPreset = false;
        _presetName = preset.Name;
        _names = new List<string>(preset.PlayerNames);
        WindowName = "Edit preset##EasyPartySortPresetEdit";
        IsOpen = true;
    }

    private static void MoveNameAt(List<string> list, string key, int newIndex)
    {
        int oldIndex = list.IndexOf(key);
        if (oldIndex == -1 || newIndex < 0 || newIndex > list.Count || oldIndex == newIndex)
            return;
        var item = list[oldIndex];
        list.RemoveAt(oldIndex);
        newIndex = Math.Clamp(newIndex, 0, list.Count);
        list.Insert(newIndex, item);
    }

    public override unsafe void Draw()
    {
        if (_preset == null && _names.Count == 0)
            return;

        ImGui.Text("Preset name:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        ImGui.InputText("##PresetName", ref _presetName, 64);
        ImGui.Separator();

        if (_names.Count == 0)
        {
            ImGui.TextDisabled("No names in this preset.");
            if (ImGui.Button("Close"))
                IsOpen = false;
            return;
        }

        ImGui.Text("Order (drag to reorder):");
        using (var child = ImRaii.Child("NameList", new Vector2(0, -40), true))
        {
            if (!child.Success)
                return;

            for (int i = 0; i < _names.Count; i++)
            {
                var name = _names[i];
                ImGui.PushID(i);
                ImGui.Selectable(name, false, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0, 28));

                if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.None))
                {
                    int len = Encoding.UTF8.GetBytes(name, 0, name.Length, _payloadBytes, 0);
                    ImGui.SetDragDropPayload(PresetNamePayloadType, new ReadOnlySpan<byte>(_payloadBytes, 0, len), ImGuiCond.None);
                    ImGui.TextUnformatted(name);
                    ImGui.EndDragDropSource();
                }

                if (ImGui.BeginDragDropTarget())
                {
                    var payload = ImGui.AcceptDragDropPayload(PresetNamePayloadType, ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                    try
                    {
                        IntPtr dataPtr = (IntPtr)payload.Data;
                        if (dataPtr != IntPtr.Zero && payload.DataSize > 0 && payload.DataSize <= _payloadBytes.Length)
                        {
                            Marshal.Copy(dataPtr, _payloadBytes, 0, (int)payload.DataSize);
                            string key = Encoding.UTF8.GetString(_payloadBytes, 0, (int)payload.DataSize);
                            MoveNameAt(_names, key, i);
                        }
                    }
                    catch (Exception) { }
                    ImGui.EndDragDropTarget();
                }
                ImGui.PopID();
            }
        }

        if (ImGui.Button("Save"))
        {
            string name = _presetName.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;
            if (_isNewPreset)
            {
                _plugin.Configuration.Presets.Add(new PartyOrderPreset
                {
                    Name = name,
                    PlayerNames = new List<string>(_names)
                });
            }
            else if (_preset != null)
            {
                _preset.Name = name;
                _preset.PlayerNames.Clear();
                _preset.PlayerNames.AddRange(_names);
            }
            _plugin.Configuration.Save();
            IsOpen = false;
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
            IsOpen = false;
    }
}
