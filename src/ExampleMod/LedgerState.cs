using System.Collections.Generic;
using Mafi.Core.Products;

namespace CoipondMod;

public static class LedgerState
{
    public static readonly Dictionary<LooseProductProto, double> Loose = new();
    public static readonly Dictionary<FluidProductProto, double> Fluids = new();
    public static readonly Dictionary<CountableProductProto, double> Items = new();

    public static void Clear()
    {
        Loose.Clear();
        Fluids.Clear();
        Items.Clear();
    }
}
