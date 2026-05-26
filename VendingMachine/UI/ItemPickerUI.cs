using System.Numerics;

namespace VendingMachine.UI;

public readonly struct ItemPickerEntry
{
    public uint ItemId { get; init; }
    public string Name { get; init; }
    public string? Detail { get; init; }

    public string Label => ItemPickerUI.FormatLabel(ItemId, Name, Detail);
}

public static class ItemPickerUI
{
    private const float ListHeight = 200f;
    private const float AddButtonWidth = 64f;

    public static string FormatLabel(uint itemId, string name, string? detail = null)
    {
        var text = $"[{itemId}] {name}";
        return string.IsNullOrEmpty(detail) ? text : $"{text} {detail}";
    }

    public static bool DrawPickerRow(
        string id,
        ref string searchFilter,
        IReadOnlyList<ItemPickerEntry> entries,
        ref int highlightedIndex,
        out bool added,
        bool allowAdd = true)
    {
        added = false;
        var preview = GetComboPreview(entries, highlightedIndex);
        var avail = ImGui.GetContentRegionAvail().X;
        var comboWidth = Math.Max(120f, avail - AddButtonWidth - ImGui.GetStyle().ItemSpacing.X);

        ImGui.PushID(id);
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("##itemPicker", preview, ImGuiComboFlags.HeightLarge))
        {
            ImGui.SetNextItemWidth(-1f);
            ImGui.InputTextWithHint("##search", UiStrings.SearchHint, ref searchFilter, 200);

            ImGui.BeginChild("##itemPickerList", new Vector2(0, ListHeight), true);
            highlightedIndex = DrawEntryList(entries, highlightedIndex);
            ImGui.EndChild();

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        ImGui.BeginDisabled(!allowAdd || highlightedIndex < 0 || highlightedIndex >= entries.Count);
        if (ImGui.Button(UiStrings.Add, new Vector2(AddButtonWidth, 0)))
            added = true;
        ImGui.EndDisabled();
        ImGui.PopID();

        return added;
    }

    private static string GetComboPreview(IReadOnlyList<ItemPickerEntry> entries, int highlightedIndex)
    {
        if (highlightedIndex >= 0 && highlightedIndex < entries.Count)
            return entries[highlightedIndex].Label;

        return UiStrings.SelectItem;
    }

    private static int DrawEntryList(IReadOnlyList<ItemPickerEntry> entries, int highlightedIndex)
    {
        if (entries.Count == 0)
        {
            ImGuiEx.Text(ImGuiColors.DalamudGrey, UiStrings.NoMatchingItems);
            return -1;
        }

        var selected = highlightedIndex;
        for (var i = 0; i < entries.Count; i++)
        {
            if (ImGui.Selectable($"{entries[i].Label}###pick{i}", selected == i))
                selected = i;
        }

        return selected;
    }
}
