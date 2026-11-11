#define VERBOSE          // comment-out for quieter log output

using HarmonyLib;
using Mafi;                           // Fix32 + Log
using Mafi.Collections;
using Mafi.Core.Entities;
using Mafi.Core.Products;
using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace CoipondMod
{
    /*───────────────────────────────────────────────────────────────────────────*\
    |*  SMALL HELPER: convert any Fix32/Duration-like object to double seconds  *|
    \*───────────────────────────────────────────────────────────────────────────*/
    internal static class Fix32Ext
    {
        // works on every public build from 0.4.x to 0.8.x (no `dynamic` – the game
        // doesn't ship a runtime Microsoft.CSharp.dll)
        public static double ToDouble(this object fx)
        {
            if (fx == null) return 0;
            if (fx is IConvertible c) return c.ToDouble(null);

            var t = fx.GetType();
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if ((m.Name == "op_Explicit" || m.Name == "op_Implicit")
                    && (m.ReturnType == typeof(double) || m.ReturnType == typeof(float))
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == t)
                    return Convert.ToDouble(m.Invoke(null, new[] { fx }));

            foreach (var n in new[] { "ToDouble", "ToFloat" })
                if (t.GetMethod(n, Type.EmptyTypes) is MethodInfo mi) return Convert.ToDouble(mi.Invoke(fx, null));

            var v = Refl.Get(fx, "AsFloat", "Float");                 // 0.7.x
            if (v != null) return Convert.ToDouble(v);

            // fallback: raw fixed-point value (1 << 12)
            var raw = Refl.Get(fx, "RawValue");
            return raw != null ? Convert.ToDouble(raw) / 4096.0 : 0;
        }
    }

    /*───────────────────────────────────────────────────────────────────────────*\
    |*  SMALL HELPER: late-bound member access by name (replaces `dynamic`)     *|
    \*───────────────────────────────────────────────────────────────────────────*/
    internal static class Refl
    {
        const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // first property/field named in `names` that exists on obj
        public static object Get(object obj, params string[] names)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            foreach (var n in names)
            {
                if (t.GetProperty(n, ALL) is PropertyInfo p && p.GetIndexParameters().Length == 0) return p.GetValue(obj);
                if (t.GetField(n, ALL) is FieldInfo f) return f.GetValue(obj);
            }
            return null;
        }

        // first collection named in `names`; works for struct elements too
        public static IEnumerable<object> GetList(object obj, params string[] names) =>
            Get(obj, names) is IEnumerable e ? e.Cast<object>() : Enumerable.Empty<object>();
    }

    /*───────────────────────────────────────────────────────────────────────────*\
    |*  MAIN PATCH: executed once every simulation tick on the sim thread.      *|
    \*───────────────────────────────────────────────────────────────────────────*/
    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    public static class LedgerCollectorPatch
    {
        const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly FieldInfo ENTITIES_F =
            AccessTools.Field(typeof(EntitiesManager), "m_entitiesLinear")
            ?? throw new Exception("m_entitiesLinear not found");

        static int _frame;   // 10 ticks/s → 60 ticks ≈ 6 s at 1x

        /*───────────────────────────────────────────────────────────────────*/
        /*  Post-fix                                                         */
        /*───────────────────────────────────────────────────────────────────*/
        static void Postfix(EntitiesManager __instance)
        {
            var ents = (LystMutableDuringIter<IEntity>)ENTITIES_F.GetValue(__instance);
            if (ents == null) return;

            if (++_frame >= 60)
            {
                DumpLedger();
                LedgerState.Clear();
                _frame = 0;
            }

            foreach (var e in ents)
            {
                var t = e.GetType();
                object r = GetRunningRecipe(e, t);
                if (r == null) continue;

                /* -------- duration (sec) -------- */
                double sec = GetDurationSeconds(r);
                if (sec < 1e-3) continue;                    // skip “instant” recipes

                double cpm = 60 / sec;                      // cycles-per-minute

                /* -------- ingredients -------- */
                foreach (var ing in Refl.GetList(r, "Ingredients", "Inputs"))
                    Add(Refl.Get(ing, "Product", "Proto") as ProductProto, -Fix32Ext.ToDouble(Refl.Get(ing, "Count", "Quantity")) * cpm);

                /* -------- products -------- */
                foreach (var prd in Refl.GetList(r, "Products", "Outputs", "Results"))
                    Add(Refl.Get(prd, "Product", "Proto") as ProductProto, Fix32Ext.ToDouble(Refl.Get(prd, "Count", "Quantity")) * cpm);
            }
        }

        /*──────────────── helper: fetch the active recipe (all builds) ───────────────*/
        static object GetRunningRecipe(object mach, Type t)
        {
            // ❶ WorkingRecipeSlot → RecipeState → Proto
            var slot = t.GetProperty("WorkingRecipeSlot", ALL)?.GetValue(mach);
            var state = slot?.GetType().GetProperty("Recipe")?.GetValue(slot);
            var proto = ToProto(state);
            if (proto != null) return proto;

            // ❷ Option<RecipeState> → RecipeState → Proto
            state = UnwrapOption(t.GetProperty("LastRecipeInProgress", ALL)?.GetValue(mach));
            proto = ToProto(state);
            if (proto != null) return proto;

            // ❸ legacy paths (unchanged)
            return
                t.GetProperty("CurrentWorkingRecipe", ALL)?.GetValue(mach) ??
                t.GetField("m_workingRecipe", ALL)?.GetValue(mach) ??
                t.GetMethod("GetCurrentRecipe", ALL)?.Invoke(mach, null) ??
                t.GetProperty("CurrentRecipe", ALL)?.GetGetMethod(true)?.Invoke(mach, null) ??
                t.GetField("m_currentRecipe", ALL)?.GetValue(mach);
        }

        /*──────────────── helper: unwrap Option<T> ────────────────────────────────*/
        static object UnwrapOption(object maybeOpt)
        {
            if (maybeOpt == null) return null;                       // null  ⇒ nothing

            var tOpt = maybeOpt.GetType();                           // Option<RecipeProto>
            var have = tOpt.GetProperty("IsSome")                    // 0.7.6a
                    ?? tOpt.GetProperty("HasValue");                 // earlier builds
            if (have == null || !(bool)have.GetValue(maybeOpt))
                return null;                                         // None ⇒ nothing

            // Value / ValueOrDefault both work – pick whichever exists
            var val = tOpt.GetProperty("Value") ?? tOpt.GetProperty("ValueOrDefault");
            return val?.GetValue(maybeOpt);
        }

        /*──────────────── helper: state → proto ───────────────────────────────────*/
        static object ToProto(object maybeState)
        {
            if (maybeState == null) return null;
            if (maybeState.GetType().Name == "RecipeProto")          // already proto?
                return maybeState;

            return maybeState.GetType().GetProperty("Proto", ALL)?.GetValue(maybeState);
        }

        /*──────────────── helper: duration in seconds (all builds) ───────────────*/
        internal static double GetDurationSeconds(object recipe)
        {
            var dur = Refl.Get(recipe, "Duration");                 // Duration struct
            if (dur != null) return Fix32Ext.ToDouble(Refl.Get(dur, "Seconds"));

            // ≥ 0.7.5 – ticks at 10 ticks/sec
            var ticks = Refl.Get(recipe, "DurationTicks");
            return ticks != null ? Fix32Ext.ToDouble(ticks) / 10 : 0;
        }

        /*──────────────── helper: accumulate flow ────────────────────────────────*/
        static void Add(ProductProto proto, double delta)
        {
            if (proto == null) return;
            if (proto is LooseProductProto lp) LedgerState.Loose[lp] = LedgerState.Loose.TryGetValue(lp, out var v) ? v + delta : delta;
            else if (proto is FluidProductProto fp) LedgerState.Fluids[fp] = LedgerState.Fluids.TryGetValue(fp, out var v) ? v + delta : delta;
            else if (proto is CountableProductProto cp) LedgerState.Items[cp] = LedgerState.Items.TryGetValue(cp, out var v) ? v + delta : delta;
        }

        /*──────────────── helper: print ledger every ~second ─────────────────────*/
        static void DumpLedger()
        {
            Log.Info("=== Resource ledger (t / min) ===");

            if (LedgerState.Loose.Count + LedgerState.Fluids.Count + LedgerState.Items.Count == 0)
            {
                Log.Info("(no active production this second)");
                return;
            }

            foreach (var kv in LedgerState.Loose) Log.Info($"{kv.Key.Strings.Name}: {kv.Value:+0.##;-0.##}");
            foreach (var kv in LedgerState.Fluids) Log.Info($"{kv.Key.Strings.Name}: {kv.Value:+0.##;-0.##}");
            foreach (var kv in LedgerState.Items) Log.Info($"{kv.Key.Strings.Name}: {kv.Value:+0.##;-0.##}");
        }
    }

    /* ------------------------------------------------------------------ *
     *  OPTIONAL one-shot reflection dump – guard with VERBOSE            *
     * ------------------------------------------------------------------ */
#if VERBOSE
    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    static class DumpEntityManagerMetadataOnce
    {
        static bool done;
        static void Prefix()
        {
            if (done) return;
            done = true;

            var t = typeof(EntitiesManager);
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                Log.Info($"[REFL] field  {f.FieldType.Name} {f.Name}");
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic))
                Log.Info($"[REFL] method {m.ReturnType.Name} {m.Name}({string.Join(", ",
                          m.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }

    [HarmonyPatch]                   // will be skipped if type not found
    static class DumpRecipeMembersOnce
    {
        const string Machine = "Mafi.Core.Machines.BlastFurnace";
        static bool Prepare() => Type.GetType(Machine) != null;

        static MethodBase TargetMethod() =>
            AccessTools.Method(Type.GetType(Machine), "simUpdate");

        static void Prefix(object __instance) { /* … */ }
    }

    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    static class ListRecipeHoldersOnce
    {
        static bool done;
        static void Prefix(EntitiesManager __instance)
        {
            if (done) return; done = true;
            var ents = (LystMutableDuringIter<IEntity>)AccessTools
                           .Field(typeof(EntitiesManager), "m_entitiesLinear").GetValue(__instance);

            foreach (var e in ents)
            {
                var t = e.GetType();
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                   .Where(f => f.Name.Contains("Recipe")))
                    Log.Info($"[{t.Name}] FIELD {f.Name} -> {f.GetValue(e)?.GetType().Name ?? "null"}");
                foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                                   .Where(p => p.Name.Contains("Recipe")))
                    Log.Info($"[{t.Name}] PROP  {p.Name} -> {p.GetValue(e)?.GetType().Name ?? "null"}");
            }
        }
    }

    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    static class DumpEntityManagerOnce
    {
        static bool done;
        static void Prefix(EntitiesManager __instance)
        {
            if (done) return;
            done = true;

            // one-shot full dump
            //DebugDump.DumpObject(__instance, "[EntitiesManager]");
        }
    }
    internal static class DebugDump
    {
        const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void DumpObject(object obj, string tag = null)
        {
            if (obj == null) { Log.Info($"[DUMP]{tag} null"); return; }

            var t = obj.GetType();
            tag = tag == null ? t.Name : $"{tag}/{t.Name}";

            foreach (var f in t.GetFields(ALL))
                Safe(() => Log.Info($"{tag}.FIELD {f.Name} = {Format(f.GetValue(obj))}"));

            foreach (var p in t.GetProperties(ALL).Where(p => p.GetIndexParameters().Length == 0))
                Safe(() => Log.Info($"{tag}.PROP  {p.Name} = {Format(p.GetValue(obj))}"));
        }

        /* ––––– helpers ––––– */

        static void Safe(Action a) { try { a(); } catch { /* ignore failures */ } }

        static string Format(object v) =>
            v == null ? "null"
            : v is IEnumerable e && !(v is string)
                ? $"[{string.Join(", ", e.Cast<object>().Take(10))}{(e.Cast<object>().Skip(10).Any() ? ", …" : "")}]"
                : v.ToString();
    }
    [HarmonyPatch(typeof(EntitiesManager), "simUpdate")]
    static class DumpAllRecipeRelatedMembersOnce
    {
        const BindingFlags ALL = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly string[] KW =
        {
        "Recipe", "Product", "Ingredient", "Input", "Output",
        "Duration", "Count", "Proto", "Items", "Fluids"
    };

        static bool done;

        static void Prefix(EntitiesManager __instance)
        {
            if (done) return;
            done = true;

            Log.Info("══════════ BEGIN *HUGE* RECIPE/PRODUCT REFLECTION DUMP ══════════");

            // gather distinct entity types currently alive
            var ents = (LystMutableDuringIter<IEntity>)AccessTools
                       .Field(typeof(EntitiesManager), "m_entitiesLinear")
                       .GetValue(__instance);

            var seen = new HashSet<Type>();
            foreach (var e in ents)
                if (seen.Add(e.GetType()))
                    DumpType(e.GetType());

            Log.Info("══════════  END  *HUGE* RECIPE/PRODUCT REFLECTION DUMP ══════════");
        }

        /*──────────────── helper ───────────────────────────────────────────*/
        static void DumpType(Type t)
        {
            Log.Info($"─── [{t.FullName}] ───");

            foreach (var f in t.GetFields(ALL))
                if (Matches(f.Name, f.FieldType.Name))
                    Log.Info($"  FIELD  {f.FieldType.Name,-25} {f.Name}");

            foreach (var p in t.GetProperties(ALL).Where(p => p.GetIndexParameters().Length == 0))
                if (Matches(p.Name, p.PropertyType.Name))
                    Log.Info($"  PROP   {p.PropertyType.Name,-25} {p.Name}");

            foreach (var m in t.GetMethods(ALL))
                if (Matches(m.Name))
                    Log.Info($"  METH   {m.ReturnType.Name,-25} {m.Name}({string.Join(", ",
                              m.GetParameters().Select(pr => pr.ParameterType.Name))})");
        }

        static bool Matches(params string[] ss) =>
            ss.Any(s => KW.Any(k => s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
    }
}
#endif
