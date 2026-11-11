// Coipond.cs  ── your Resource-Ledger mod’s root class
using HarmonyLib;
using Mafi;
using Mafi.Base;
using Mafi.Core;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;   // for ProtoRegistrator

namespace CoipondMod;          // namespace can be anything

public sealed class Coipond : DataOnlyMod
{
    public override string Name => "Resource Ledger";
    public override int Version => 1;

    // Constructor lists dependencies exactly like ExampleMod.
    // They’re injected by the game – you don’t need to use the variables
    // (but having them proves the mods were loaded first).
    public Coipond(CoreMod coreMod, BaseMod baseMod)
    {
        Log.Info($"{Name}: constructed");

        // Harmony setup – unique ID for your mod!
        var harmony = new Harmony("com.yourname.coi.resourceledger");
        harmony.PatchAll();          // apply every [HarmonyPatch] in this DLL
    }

    // Required abstract member – even if you register nothing yet.
    public override void RegisterPrototypes(ProtoRegistrator registrator)
    {
        // No prototypes to add now, leave it empty.
    }
}
