// Coipond.cs  ── your Resource-Ledger mod’s root class
using HarmonyLib;
using Mafi;
using Mafi.Collections;
using Mafi.Core.Mods;

namespace CoipondMod;          // namespace can be anything

public sealed class Coipond : DataOnlyMod
{
    // Name/version now come from manifest.json (Update 4+).
    public Coipond(ModManifest manifest) : base(manifest)
    {
        Log.Info("Coipond: constructed");

        // Harmony setup – unique ID for your mod!
        var harmony = new Harmony("com.jtturbo.coi.coipond");
        harmony.PatchAll();          // apply every [HarmonyPatch] in this DLL
    }

    // Required abstract member – even if you register nothing yet.
    public override void RegisterPrototypes(ProtoRegistrator registrator)
    {
        // No prototypes to add now, leave it empty.
    }

    public override void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
    }
}
