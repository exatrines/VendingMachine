namespace VendingMachine.UI;

internal sealed class ItemPickerState
{
    public string Filter = "";
    public int HighlightedIndex = -1;

    public void Reset()
    {
        Filter = "";
        HighlightedIndex = -1;
    }

    public void ClampHighlight(IReadOnlyList<ItemPickerEntry> entries)
    {
        if (HighlightedIndex >= entries.Count)
            HighlightedIndex = -1;
    }

    public bool TryAdd(string id, IReadOnlyList<ItemPickerEntry> entries, bool allowAdd = true)
    {
        ClampHighlight(entries);
        return ItemPickerUI.DrawPickerRow(id, ref Filter, entries, ref HighlightedIndex, out var added, allowAdd)
               && added;
    }
}
