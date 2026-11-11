// Coipond.cs  ── your Resource-Ledger mod’s root class
using Mafi;
using Mafi.Collections;
using Mafi.Core.Mods;
using Mafi.Core.Prototypes;

namespace CoipondMod;          // namespace can be anything

// IMod is re-declared so RegisterDependencies below can replace DataOnlyMod's
// non-virtual implementation (C# interface re-implementation).
public sealed class Coipond : DataOnlyMod, IMod
{
    // Name/version now come from manifest.json (Update 4+).
    public Coipond(ModManifest manifest) : base(manifest)
    {
        Log.Info("Coipond: constructed");
    }

    // Required abstract member – even if you register nothing yet.
    public override void RegisterPrototypes(ProtoRegistrator registrator)
    {
        // No prototypes to add now, leave it empty.
    }

    // The game constructs LedgerService and injects its constructor dependencies.
    void IMod.RegisterDependencies(DependencyResolverBuilder depBuilder, ProtosDb protosDb, bool gameWasLoaded)
    {
        RegisterDependencies(depBuilder, protosDb, gameWasLoaded);
        depBuilder.RegisterDependency<LedgerService>().AsSelf();
    }

    public override void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues)
    {
    }
}
