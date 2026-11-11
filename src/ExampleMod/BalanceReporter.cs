// BalanceReporter.cs   – drop-in, recompile, done.
using HarmonyLib;
using Mafi;                             // Log / Fix32
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Products;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CoipondMod
{
    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    internal static class BalanceReporter
    {
        /* ─── CONFIG ─── */
        const int TICKS_INTERVAL = 600;      // 10 ticks/s → every ~60 s at 1x
        const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly FieldInfo ENTITIES_F =
            AccessTools.Field(typeof(EntitiesManager), "m_entitiesLinear")
            ?? throw new Exception("m_entitiesLinear not found");

        /* ─── STATE ─── */
        static int ticks;

        /* ─── PATCH ─── */
        static void Postfix(EntitiesManager __instance)
        {
            if (++ticks < TICKS_INTERVAL) return;
            ticks = 0;
            DumpNow(__instance);
        }

        /* ───────────────────────────── public helper ───────────────────────────── */
        public static void DumpNow(EntitiesManager mgr)
        {
            var ents = (LystMutableDuringIter<IEntity>)ENTITIES_F.GetValue(mgr);
            if (ents == null) return;

            var grand = new Dictionary<ProductProto, double>();
            var perEnt = new List<Tuple<string, Dictionary<ProductProto, double>>>();

            foreach (var e in ents)
            {
                var flow = GetEntityFlow(e);
                if (flow.Count == 0) continue;

                perEnt.Add(Tuple.Create($"{e.GetType().Name} #{e.Id}", flow));

                foreach (var kv in flow)
                    grand[kv.Key] = grand.TryGetValue(kv.Key, out var v) ? v + kv.Value : kv.Value;
            }

            /* ── LOG ── */
            Log.Info("=== CAPACITY BALANCE (t/min) ===");
            foreach (var kv in grand.OrderBy(k => k.Key.Id))
                Log.Info($"{kv.Key.Strings.Name,-18} {kv.Value:+0.##;-0.##}");

            Log.Info("── Top producers/consumers ──");
            foreach (var tuple in perEnt
                     .OrderByDescending(t => t.Item2.Values.Sum(Math.Abs))
                     .Take(8))
            {
                var parts = string.Join("  ",
                    tuple.Item2.Select(kv => $"{kv.Key.Strings.Name} {(kv.Value >= 0 ? "+" : "")}{kv.Value:0.#}"));
                Log.Info($"{tuple.Item1,-22} {parts}");
            }
        }

        /* ───────────────────────────── helpers ───────────────────────────── */

        static Dictionary<ProductProto, double> GetEntityFlow(IEntity ent)
        {
            var flow = new Dictionary<ProductProto, double>();
            var tEnt = ent.GetType();

            foreach (var recipe in GetRecipes(ent, tEnt))
            {
                double sec = LedgerCollectorPatch.GetDurationSeconds(recipe);
                if (sec < 1e-3) continue;                    // skip “instant” recipes
                double cpm = 60.0 / sec * GetDurationMult(ent, tEnt);

                foreach (var pq in Refl.GetList(recipe, "Inputs", "Ingredients"))
                    Add(flow, Refl.Get(pq, "Product", "Proto") as ProductProto, -Fix32Ext.ToDouble(Refl.Get(pq, "Count", "Quantity")) * cpm);

                foreach (var pq in Refl.GetList(recipe, "Results", "Products", "Outputs"))
                    Add(flow, Refl.Get(pq, "Product", "Proto") as ProductProto, Fix32Ext.ToDouble(Refl.Get(pq, "Count", "Quantity")) * cpm);
            }
            return flow;
        }

        /* active / assigned recipes (works for every machine) */
        static IEnumerable<object> GetRecipes(object e, Type t)
        {
            // multi-recipe machines (RecipesAssigned)
            var assigned = Refl.GetList(e, "RecipesAssigned").ToList();
            if (assigned.Count > 0) return assigned;

            // single recipe – WorkingRecipeSlot / LastRecipeInProgress / legacy
            object tryProto(object obj) =>
                obj == null ? null
                : obj.GetType().Name == "RecipeProto" ? obj
                : obj.GetType().GetProperty("Proto", ALL)?.GetValue(obj);

            var slot = t.GetProperty("WorkingRecipeSlot", ALL)?.GetValue(e);
            var proto =
                tryProto(slot?.GetType().GetProperty("Recipe")?.GetValue(slot)) ??
                tryProto(UnwrapOption(t.GetProperty("LastRecipeInProgress", ALL)?.GetValue(e))) ??
                tryProto(t.GetProperty("CurrentRecipe", ALL)?.GetValue(e));

            return proto == null ? Array.Empty<object>() : new[] { proto };
        }

        static double GetDurationMult(object ent, Type tEnt)
        {
            var p = tEnt.GetProperty("DurationMultiplier")   // Machines
                  ?? tEnt.GetProperty("DurationMultForCurrentJourney"); // ships
            return p != null ? Fix32Ext.ToDouble(p.GetValue(ent)) : 1;
        }

        static void Add(Dictionary<ProductProto, double> dict, ProductProto p, double d)
        {
            if (p != null) dict[p] = dict.TryGetValue(p, out var v) ? v + d : d;
        }

        static object UnwrapOption(object opt)
        {
            if (opt == null) return null;
            var t = opt.GetType();
            var has = t.GetProperty("IsSome") ?? t.GetProperty("HasValue");
            if (has == null || !(bool)has.GetValue(opt)) return null;
            var val = t.GetProperty("Value") ?? t.GetProperty("ValueOrDefault");
            return val?.GetValue(opt);
        }
    }
}
