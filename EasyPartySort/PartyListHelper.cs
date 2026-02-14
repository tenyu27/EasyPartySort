using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace EasyPartySort;

/// <summary>
/// Gets party list in the same order as the in-game party list UI.
/// IPartyList is ordered by spawn index; the actual display order is from AgentHUD (HudPartyMember.Index).
/// </summary>
public static unsafe class PartyListHelper
{
    private const char KeySep = '\x01';

    // CharacterData.ClassJob offset within Character (GameObject size 0x1A0 + CharacterData.ClassJob 0x2A)
    private const int ClassJobOffset = 0x1A0 + 0x2A;
    private const int LevelOffset = 0x1A0 + 0x2B;

    /// <summary>Stable key for matching entries (same as MainWindow).</summary>
    public static string GetMemberKey(PartyMemberEntry m) => $"{m.Name}{KeySep}{m.JobAbbr}{KeySep}{m.Level}";

    public sealed class PartyMemberEntry
    {
        public int DisplayIndex { get; init; }
        public string Name { get; init; } = "";
        public string JobAbbr { get; init; } = "";
        public byte Level { get; init; }
        /// <summary>Lumina ClassJob row Icon ID for job icon texture.</summary>
        public uint IconId { get; init; }
    }

    /// <summary>
    /// Returns party members in the same order as shown in the in-game party list (sorted by HUD Index).
    /// Returns empty list if not in a party or on error.
    /// </summary>
    public static List<PartyMemberEntry> GetPartyListInDisplayOrder(IDataManager dataManager)
    {
        var result = new List<PartyMemberEntry>();
        if (dataManager == null)
            return result;

        try
        {
            var agent = AgentHUD.Instance();
            if (agent == null)
                return result;

            var classJobSheet = dataManager.GetExcelSheet<ClassJob>();
            if (classJobSheet == null)
                return result;

            var members = new List<(byte Index, string Name, string JobAbbr, byte Level, uint IconId)>();

            foreach (ref var partyMember in agent->PartyMembers)
            {
                if (partyMember.Object == null)
                    continue;

                byte index = partyMember.Index;
                string name = partyMember.Name.ToString();
                if (string.IsNullOrEmpty(name))
                    name = "(unknown)";

                // BattleChara layout: GameObject (0x1A0) then CharacterData (ClassJob at 0x2A, Level at 0x2B)
                byte* ptr = (byte*)partyMember.Object;
                byte classJobId = ptr[ClassJobOffset];
                byte level = ptr[LevelOffset];

                var jobRow = classJobSheet.GetRow((uint)classJobId);
                string jobAbbr = jobRow.Abbreviation.ToString();
                if (string.IsNullOrEmpty(jobAbbr))
                    jobAbbr = classJobId.ToString();
                // FFXIV class/job icons in game UI are typically in the 062000 range (base 62000 + row)
                uint iconId = 62000u + (uint)classJobId;

                members.Add((index, name, jobAbbr, level, iconId));
            }

            members.Sort((a, b) => a.Index.CompareTo(b.Index));

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                result.Add(new PartyMemberEntry
                {
                    DisplayIndex = i + 1,
                    Name = m.Name,
                    JobAbbr = m.JobAbbr,
                    Level = m.Level,
                    IconId = m.IconId
                });
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Error(ex, "PartyListHelper.GetPartyListInDisplayOrder failed");
        }

        return result;
    }

    /// <summary>
    /// Applies the desired party order to the game using InfoProxyPartyMember.ChangeOrder.
    /// </summary>
    public static void ApplyPartyOrder(List<PartyMemberEntry> desiredOrder)
    {
        if (desiredOrder == null || desiredOrder.Count == 0)
            return;
        try
        {
            var proxy = InfoProxyPartyMember.Instance();
            if (proxy == null)
                return;
            int count = (int)proxy->GetEntryCount();
            if (count != desiredOrder.Count)
                return;

            var desiredKeys = desiredOrder.Select(GetMemberKey).ToList();

            for (int i = 0; i < count; i++)
            {
                GetCurrentOrderWithIndices(out var currentIndices, out var currentKeys);
                if (currentIndices == null || currentKeys == null || currentKeys.Count != count)
                    break;
                if (currentKeys[i] == desiredKeys[i])
                    continue;
                int swapIndex = -1;
                for (int j = i + 1; j < count; j++)
                {
                    if (currentKeys[j] == desiredKeys[i])
                    {
                        swapIndex = j;
                        break;
                    }
                }
                if (swapIndex == -1)
                    continue;
                proxy->ChangeOrder(currentIndices[swapIndex], i);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.Error(ex, "PartyListHelper.ApplyPartyOrder failed");
        }
    }

    /// <summary>Gets current display order: game indices and keys (by Index).</summary>
    private static unsafe void GetCurrentOrderWithIndices(out List<int>? indices, out List<string>? keys)
    {
        indices = null;
        keys = null;
        var agent = AgentHUD.Instance();
        if (agent == null)
            return;
        var members = new List<(byte Index, string Key)>();
        foreach (ref var partyMember in agent->PartyMembers)
        {
            if (partyMember.Object == null)
                continue;
            string name = partyMember.Name.ToString();
            if (string.IsNullOrEmpty(name))
                name = "(unknown)";
            byte* ptr = (byte*)partyMember.Object;
            byte classJobId = ptr[ClassJobOffset];
            byte level = ptr[LevelOffset];
            string jobAbbr = classJobId.ToString();
            try
            {
                var sheet = Plugin.DataManager?.GetExcelSheet<ClassJob>();
                if (sheet != null)
                {
                    var row = sheet.GetRow((uint)classJobId);
                    jobAbbr = row.Abbreviation.ToString();
                }
            }
            catch { /* ignore */ }
            string key = $"{name}{KeySep}{jobAbbr}{KeySep}{level}";
            members.Add((partyMember.Index, key));
        }
        members.Sort((a, b) => a.Index.CompareTo(b.Index));
        indices = members.Select(m => (int)m.Index).ToList();
        keys = members.Select(m => m.Key).ToList();
    }
}
