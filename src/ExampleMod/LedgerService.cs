// LedgerService.cs  ── builds the resource ledger from the game's own product stats
using System.Collections.Generic;
using System.Linq;
using Mafi;
using Mafi.Core.Products;
using Mafi.Core.Prototypes;
using Mafi.Core.Simulation;
using Mafi.Core.Stats;

namespace CoipondMod;

/// <summary>One product's flow over the last game month (= 60 s at 1x speed).</summary>
public sealed class LedgerEntry
{
    public readonly ProductProto Product;
    public readonly long Produced;
    public readonly long Consumed;
    public long Net => Produced - Consumed;

    /// <summary>Building types that produced / consumed this product, largest first.</summary>
    public readonly KeyValuePair<Proto, long>[] ProducedBy;
    public readonly KeyValuePair<Proto, long>[] ConsumedBy;

    public LedgerEntry(ProductProto product, long produced, long consumed,
                       KeyValuePair<Proto, long>[] producedBy, KeyValuePair<Proto, long>[] consumedBy)
    {
        Product = product;
        Produced = produced;
        Consumed = consumed;
        ProducedBy = producedBy;
        ConsumedBy = consumedBy;
    }
}

/// <summary>
/// Reads per-product created/used totals that the game already tracks
/// (IProductsManager.ProductStats) once per game month and publishes an
/// immutable snapshot. No Harmony, no per-tick entity scanning.
/// </summary>
public sealed class LedgerService
{
    readonly IProductsManager m_productsManager;

    // Swapped atomically on the sim thread; the array is never mutated after
    // publishing, so the UI thread can read it without locking.
    volatile LedgerEntry[] m_snapshot = new LedgerEntry[0];

    /// <summary>Latest ledger, products with any activity, sorted by net (deficits first).</summary>
    public IReadOnlyList<LedgerEntry> Snapshot => m_snapshot;

    public LedgerService(IProductsManager productsManager, ICalendar calendar)
    {
        m_productsManager = productsManager;
        // Non-saveable: a saveable subscription would write this mod's types into the save.
        calendar.NewMonthEnd.AddNonSaveable(this, rebuild);
        rebuild();   // show last month's data right after loading a save
        Log.Info("Coipond: ledger service ready");
    }

    void rebuild()
    {
        var entries = new List<LedgerEntry>();
        foreach (var stats in m_productsManager.ProductStats)
        {
            long produced = stats.CreatedTotalStats.LastMonth.Value;
            long consumed = stats.UsedTotalStats.LastMonth.Value;
            if (produced == 0 && consumed == 0) continue;

            entries.Add(new LedgerEntry(stats.Product, produced, consumed,
                byBuilding(stats.ProducedBy), byBuilding(stats.ConsumedBy)));
        }

        m_snapshot = entries.OrderBy(e => e.Net).ToArray();
        logLedger();
    }

    static KeyValuePair<Proto, long>[] byBuilding(IReadOnlyDictionary<Proto, QuantitySumStats> dict) =>
        dict.Select(kv => new KeyValuePair<Proto, long>(kv.Key, kv.Value.LastMonth.Value))
            .Where(kv => kv.Value != 0)
            .OrderByDescending(kv => kv.Value)
            .ToArray();

    // Temporary until the UI panel exists: lets us compare against the game's stats window.
    void logLedger()
    {
        var snapshot = m_snapshot;
        Log.Info($"=== Resource ledger, last month (per 60 s at 1x), {snapshot.Length} products ===");
        foreach (var e in snapshot)
            Log.Info($"{e.Product.Strings.Name,-24} net {e.Net,8:+0;-0;0}   made {e.Produced,7}   used {e.Consumed,7}");
    }
}
