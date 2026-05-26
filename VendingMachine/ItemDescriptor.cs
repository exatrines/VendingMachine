using ECommons.ExcelServices;

namespace VendingMachine;

public struct ItemDescriptor : IEquatable<ItemDescriptor>
{
    public int Id;
    public bool HQ;

    public ItemDescriptor(uint id, bool hq)
    {
        Id = (int)id;
        HQ = hq;
    }

    public bool Equals(ItemDescriptor other) => Id == other.Id && HQ == other.HQ;

    public override bool Equals(object? obj) => obj is ItemDescriptor d && Equals(d);

    public override int GetHashCode() => HashCode.Combine(Id, HQ);

    public override readonly string ToString() => $"[{ExcelItemHelper.GetName((uint)Id, true)},{HQ}]";

    public static bool operator ==(ItemDescriptor left, ItemDescriptor right) => left.Equals(right);

    public static bool operator !=(ItemDescriptor left, ItemDescriptor right) => !left.Equals(right);
}
