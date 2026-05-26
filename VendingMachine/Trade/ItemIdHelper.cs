namespace VendingMachine.Trade;

internal static class ItemIdHelper
{
    public const int HqOffset = 1_000_000;

    public static uint Normalize(uint itemId) => itemId % (uint)HqOffset;

    public static bool IsHq(uint rawItemId) => rawItemId >= HqOffset;
}
