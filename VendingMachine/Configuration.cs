namespace VendingMachine;

public enum TradeMode
{
    Sell,
    Buy,
}

public sealed class SellEntry
{
    public uint ItemId;
    public int Count = 1;
    public int PriceGil;
}

public sealed class BuyEntry
{
    public uint ItemId;
    public int PricePerStackGil;
}

public sealed class Configuration
{
    public bool Enabled;
    public TradeMode Mode = TradeMode.Sell;
    public List<SellEntry> SellEntries = [];
    public List<BuyEntry> BuyEntries = [];
}
