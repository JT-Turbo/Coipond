# Captain of Industry modding

Welcome to the official modding resource for the [Captain of Industry](https://captain-of-industry.com) game.

## Status of modding support

COI is currently [available on Steam](https://store.steampowered.com/app/1594320/Captain_of_Industry/?utm_source=GitHubModding) and it is in Early Access stage.
While modding is possible, it is not officially supported yet.
We are continuously working on improving modding support and documentation.

Before getting started, please familiarize yourself with our [modding policy](https://www.captain-of-industry.com/modding-policy).

## Questions, issues

Note that mod support is experimental and APIs might have breaking changes.
If you are having issues, always examine the logs in the `%APPDATA%/Captain of Industry/Logs` directory, they contain a lot of useful information.
If you'd like to discuss modding topics with the community and devs, visit our [Discord channel #modding-dev-general](https://discord.gg/JxmUbGsNRU) (you will need to assign yourself a `Mod creation` role in `#pick-your-roles-here` channel).
You can also file issues here on GitHub, but the response time from our team might be delayed.

## Prerequisites

In order to start modding COI, you will need to:

1. Own Captain of Industry on Steam and have the game installed.
2. Have .NET Framework 4.8 installed.
3. (optional) Have Unity 6000.0.66f1 installed (needed only for asset creation).

## Getting started

1. Locate the COI game files via right-click on the game title in the Steam client -> `Properties...` -> `Local Files` -> `Browse`.
2. Copy the game root path to your clipboard (e.g. `C:/Steam/steamapps/common/Captain of Industry`).
3. Create a new environment variable called `COI_ROOT` and set its value to the game root path copied above. On Windows, use the handy `Edit environment variables` tool, just open the Start menu and type `Edit env` and you should see it.
4. Fork/download this repo.
5. Compile the `ExampleMod` in the `Release` configuration located at `src/ExampleMod/ExampleMod.slnx` (or `ExampleMod.sln`). We recommend to use [Visual Studio](https://visualstudio.microsoft.com/) but feel free to use any other tools, such as the `dotnet build` console command. Note that `dotnet build` may not work with the build automation tasks - Visual Studio is recommended. If you are seeing a lot of errors, check your `COI_ROOT` environment variable, try restarting.
6. The build automatically deploys to the mods folder (see [Automatic deployment](#automatic-deployment)). The resulting `ExampleMod.dll` can also be found in `bin/Release/net48`.
7. Launch the game, create a new game, and observe that the `ExampleMod` is in the list of available mods. Select it and create the new game. You can ensure that the mod was loaded by locating a new node in the research tree (open using `G` key). In case of any errors, examine logs in the `%APPDATA%/Captain of Industry/Logs` directory.
8. Congratulations, you are now running your mod in Captain of Industry!

## Mod structure

A mod is a directory containing at minimum a `manifest.json` and a compiled DLL. The directory name must match the mod's `id` field in the manifest. Below is the expected layout:

```
ExampleMod/
  manifest.json           (required - mod metadata)
  config.json             (optional - player-configurable options)
  readme.txt              (optional - installation instructions for players)
  ExampleMod.dll          (required - compiled mod assembly)
  ExampleMod.pdb          (optional - debug symbols)
  AssetBundles/           (optional - icons, 3D models, etc.)
```

## Mod manifest (`manifest.json`)

Every mod must include a `manifest.json` file in its root directory. The manifest defines mod metadata, dependencies, and loading behavior.

A JSON schema file (`manifest.schema.json`) is included in the ExampleMod project for IDE IntelliSense support.

### Required fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique mod identifier. Must match `[a-zA-Z0-9][a-zA-Z0-9_-]*`. Must not start with `COI-` (reserved for official content). |
| `version` | string | Mod version (e.g. `0.0.1`, `1.2.3a`). Follows the pattern `major.minor[.patch[letter]]`. |
| `primary_dlls` | string[] | List of DLL file names to load (DLLs will be loaded in the provided order). All loaded DLLs must be located in the mod's directory. |

### Optional fields

| Field | Type | Description |
|-------|------|-------------|
| `display_name` | string | Human-readable name shown in UI (max 50 characters). |
| `description_short` | string | Short description (max 180 characters). |
| `description_long` | string | Detailed description shown in mod details. |
| `authors` | string or string[] | Author name or array of author names. |
| `min_game_version` | string | Minimum compatible game version (e.g. `0.8.0`). |
| `max_verified_game_version` | string | Highest game version the mod has been tested with. |
| `links` | string[] | Web links (GitHub, YouTube, etc.). |
| `mod_dependencies` | string[] | Required mod IDs. Supports version constraints (e.g. `OtherMod >= 1.0.0`). |
| `optional_mod_dependencies` | string[] | Optional mod IDs with same version constraint syntax. |
| `incompatible_mods` | string[] | Mod IDs that cannot be loaded together with this mod. |
| `non_locking_dll_load` | bool | If `true`, DLLs are loaded into memory instead of locked on disk. Allows updating files without closing the game. |
| `can_add_to_saved_game` | bool | If `true`, the mod can be added to an existing save. |
| `can_remove_from_saved_game` | bool | If `true`, the mod can be removed from an existing save. |
| `primary_mod_class_name` | string | Class name to use when multiple `IMod` implementations are found in the loaded DLLs. |

### Example manifest

```json
{
  "id": "ExampleMod",
  "version": "0.0.1",
  "display_name": "Example Mod",
  "description_short": "A simple example mod for Captain of Industry.",
  "authors": "Your Name",
  "primary_dlls": ["ExampleMod.dll"],
  "non_locking_dll_load": true
}
```

### Dependencies

Mods are loaded in dependency order (topologically sorted).
If your mod depends on another mod, declare it in `mod_dependencies`:

```json
{
  "mod_dependencies": ["SomeLibraryMod", "AnotherMod >= 2.0.0"]
}
```

Dependencies with version constraints use the `>=` syntax (e.g. `ModId >= 1.0.0`). Spaces around `>=` are recommended for readability.
All mandatory dependencies must be present and loaded, or the game will report an error.
Optional dependencies (`optional_mod_dependencies`) are loaded first if available but do not cause errors if missing.

## Mod configuration (`config.json`)

Mods can provide a `config.json` file to expose player-configurable options in the game's settings UI.
A JSON schema file (`config.schema.json`) is included for IDE IntelliSense support.

### Supported parameter types

| Type | `default` value | Extra fields/constraints |
|------|----------------|--------------|
| Boolean | `true` / `false` | - |
| String | `"text"` | `max_length`, `regex` |
| Integer | `42` | `min`, `max`, `is_integer` (must be `true`) |
| Float | `5.3` | `min`, `max` |

Parameter names must be in `snake_case` format.

### Example config

```json
{
  "enable_feature": {
    "default": true,
    "description": "Enable the custom feature."
  },
  "production_multiplier": {
    "default": 42,
    "min": 1,
    "max": 100,
    "is_integer": true,
    "description": "Production multiplier for custom recipes."
  }
}
```

### Accessing config values in code

```csharp
int multiplier = JsonConfig.GetInt("production_multiplier");
bool enabled = JsonConfig.GetBool("enable_feature");

// React to changes made by the player in settings UI
JsonConfig.OnValueChanged += paramName => { /* handle change */ };
```

Config values are persisted in save files.
Use `MigrateJsonConfig()` to handle schema changes between mod versions.

## Mod implementation

Your mod must contain exactly one class implementing the `IMod` interface (from `Mafi.Core.Mods`).
For mods that only add content (machines, recipes, research, etc.) without custom services, use the `DataOnlyMod` base class:

```csharp
public sealed class ExampleMod : DataOnlyMod {

  public ExampleMod(ModManifest manifest) : base(manifest) {
    Log.Info("ExampleMod: constructed");
  }

  public override void RegisterPrototypes(ProtoRegistrator registrator) {
    registrator.RegisterAllProducts();
    registrator.RegisterData<ExampleMachineData>();
    registrator.RegisterDataWithInterface<IResearchNodesData>();
  }

  public override void MigrateJsonConfig(VersionSlim savedVersion, Dict<string, object> savedValues) {
  }
}
```

For mods that need to register custom dependencies or perform initialization, implement `IMod` directly.
The lifecycle methods are called in this order:

1. `RegisterPrototypes()` - register all game content (machines, recipes, etc.)
2. `RegisterDependencies()` - register custom services with the DI container
3. `EarlyInit()` - early initialization before map generation
4. `Initialize()` - final initialization before game starts

## Automatic deployment

The `ExampleMod.csproj` includes MSBuild targets that automatically deploy the mod on every build (controlled by the `DeployToModsFolder` property in the csproj, `true` by default):

- Copies `manifest.json`, `config.json`, `readme.txt`, the compiled DLL, and asset bundles to `%APPDATA%/Captain of Industry/Mods/<ModName>/`.
- Creates a distributable zip file in the mods folder. The zip contains the mod directory as its root, so players can extract it directly into the `Mods` folder.
- Debug builds also copy PDB files for debugging.

The deployment target path is controlled by the `COI_MODS` variable in the csproj, which defaults to `%APPDATA%/Captain of Industry/Mods`.

### Version auto-generation

The assembly version is automatically generated from the `version` field in `manifest.json`.
You do not need to set version attributes manually.
The version `1.2.3a` is normalized to assembly version `1.2.3.1` (the letter suffix is converted to a numeric revision: a=1, b=2, etc.).

## DLC references

If your mod needs to reference DLC content, set the corresponding flags in the csproj:

```xml
<RequiresSupporterDlc>true</RequiresSupporterDlc>
<RequiresTrainsDlc>true</RequiresTrainsDlc>
```

This adds references to `Mafi.Supporter.dll` and `Mafi.TrainsDlc.dll` respectively.
Remember to declare the DLC mod as a dependency in your `manifest.json` if your mod requires it.

## Assets creation

Assets such as icons or 3D models can be created using the Unity editor. We currently use Unity 6000.0.66f1 and it is recommended to use the same version to avoid incompatibilities.

### Unity setup

One-time Unity setup needed for MaFi tools to function properly.

1. Download and install Unity 6000.0.66f1 from the [Unity download archive](https://unity.com/releases/editor/archive).
2. Locate the test scene in `src/ExampleMod.Unity`. Do not open it yet.
3. Create a directory link called `UnityRootSymlink` in `src\ExampleMod.Unity\Library` that points to the Unity version installation folder. This can be done by invoking `mklink /D <target> <source>` command in console window with admin privileges. For example: `mklink /D "C:\CaptainOfIndustryModding\src\ExampleMod.Unity\Library\UnityRootSymlink" "C:\Program Files\Unity\Hub\Editor\6000.0.66f1"`. When you navigate to the `UnityRootSymlink` you should see a single `Editor` directory in it.
4. Create hard-links for necessary DLLs from your Steam installation by running the `src/ExampleMod.Unity/Assets/DLLs/create_dll_hardlinks.bat` batch file. You will need to run it under admin privileges (right-click, Run as Administrator).
   - It is a good practice to look at any code you are running under admin privileges, so feel free to inspect the batch file first.
   - Alternatively, you could also copy the DLLs in question to this directory but hard link is better since any update to the original files will propagate.
5. Open the test scene from `src/ExampleMod.Unity/Assets/ExampleModScene.unity` in the Unity Editor. This can be done via Unity Hub by selecting `Open project from disk` in the `Projects` tab. Make sure you select the right Unity version if you have multiple installed.
6. Verify that you can see `MaFi` in the top menu on the Unity editor. If not, linked DLLs were not properly loaded and you will not be able to create assets.
7. Open the `ExampleModScene` by double-clicking on it in the `Project` pane (it's under `Assets` directory).
8. (optional) We also recommend changing the following settings in the Unity editor (`Edit` -> `Preferences`).
   - `External tools` -> `External script editor` -> `Open by file extension`. This will stop regenerating project files and placing absolute paths instead of relative.
   - `General` -> `Disable Editor Analytics` (if you can and want).

### Creation of icon assets

Following steps describe how to package icons, for example for new products.

1. We recommend organizing assets in directories. Under the `Assets` directory create `<mod name>/<icons category>` directory, in our case that is `ExampleMod/ProductIcons`.
2. Import images as png or jpg files to the newly created directory.
3. Configure newly imported textures to have `Sprite (2D and UI)` type and apply the change.
4. Assign the newly imported textures to any asset bundle from the drop-down menu on the bottom of the Inspector tab. You can create a new bundle called `asdf` or pick any existing one.
5. To use an icon in your mod (for example as a product icon), simply right-click on a texture and select `Copy Path`. That path can be used to load your prefab in the game.
6. Follow steps under the [Packaging asset bundles](#packaging-asset-bundles) to package the created assets.

Note: Unlike 3D models, textures do not need to have a `prefab` created.

### Creation of 3D model asset

Following steps describe how to create a 3D model template that is very beneficial in creation of 3D models of buildings.

1. Define a layout entity in your mod with desired layout specified using ASCII (see `ExampleMachineData.cs`).
2. Set prefab path to `"TODO"` since we don't have a prefab yet.
3. Compile and deploy the mod.
4. Launch game with the newly created machine and run console command `generate_layout_entity_mesh_template` followed by your entity ID. This will generate an OBJ file in `%APPDATA%/Captain of Industry/Misc/GeneratedMeshTemplates` which represents a 3D bounding box of layout of your new entity with exact port locations.
5. If you don't have a 3D model, load the newly created template model to a 3D editor of your choice and create 3D model that fits it. If you already have a 3D model, you can compare it to the generated template and edit the ASCII layout accordingly.
6. When 3D model is complete, export FBX or OBJ and follow the next steps.

Following steps describe how to package a 3D model.

1. We recommend organizing assets in directories. Under the `Assets` directory create `<mod name>/<model name>` directory, in our case that is `ExampleMod/ExampleModel`.
2. Import 3D model files (OBJ, FBX, etc.) and textures (PNG, JPG, etc) to the model directory. You can simply use drag & drop. Creating a separate folder for each asset is recommended.
3. Drag the 3D model from Project pane to the Unity scene. Reset its position and rotation to all zeros using Inspector (you can use the three dots menu next to transform) and make it look as you want (add materials, etc.). Note: it is important that your asset has zero position and rotation. If you need to reposition it, change the 3D model or make a child object that can be moved.
4. Create a prefab by dragging the root object and dropping it to the project pane. This will make a new `.prefab` file. In our example we created the prefab in the `<mod name>` directory.
5. Assign the newly created prefab to any asset bundle from the drop-down menu on the bottom of the Inspector tab. You can create a new temporary bundle called `asdf` or pick any existing one.
6. To use prefabs in your mod, simply right-click any prefab and select `Copy Path`. That path can be used to load your prefab in the game.
7. Follow steps under the [Packaging asset bundles](#packaging-asset-bundles) to package the created assets.

### Packaging asset bundles

Once your assets are ready, follow these steps to package them with your mod.
Mods are published in a directory matching the mod's `id`, in our case `ExampleMod/`.
Now we can add asset bundles to the same directory.

1. Ensure that everything in Unity is saved (use `Ctrl+S`).
2. Build asset bundles by right-clicking anywhere in the project pane (on any file or empty area) and select `[MaFi] Build asset bundles`. After Unity is done processing, asset bundle files can be found in the `src/ExampleMod.Unity/AssetBundles` directory.
3. Asset bundles are automatically copied to the mod deployment folder on the next build (see [Automatic deployment](#automatic-deployment)). You can also manually copy contents of `src/ExampleMod.Unity/AssetBundles` to the `AssetBundles` folder next to the mod DLL.
4. (optional) If you want to make it neat, you really only need the asset bundles (files in format `YourPrefabName_xxxx`, without extension) and the `mafi_bundles.manifest` file. All other `.manifest` files could be removed as well as the `AssetBundles` file.

If you make any changes to your prefabs, simply rebuild asset bundles and use the new files from the `AssetBundles` directory.

## Running game directly in Unity

It is possible to run the game directly in the Unity editor.
This allows you to inspect the running game's scene, inspect and debug UI, validate colliders, run performance analysis, and more.
Note that the game uses instancing heavily and most meshes in the game are not GameObjects, thus are not selectable.

This is how to make it work for you:

1. Complete the [Unity setup](#unity-setup) guide in Assets creation above.
2. Select the `Game` object in the Hierarchy tab.
3. In the Inspector tab, you should see 3 entries with missing script references, replace them as follows (if not, just add new ones):
  - First: `GameMainMb`
  - Second: `GameDebugConfigStatelessMb`
  - Third: `GameDebugConfig`
4. In the `GameMainMb`, set the `Working Dir Path Override` to `%COI_ROOT%`, and the `Core Asset Bundle Path` to `%COI_ROOT%\AssetBundles` (`COI_ROOT` is set up in [Getting started](#getting-started)).
5. In the `GameDebugConfigStatelessMb` options, check `Skip splash screen`.
6. Switch to the Game view and make sure that under Aspect ratio settings (top bar), the `Low Resolution Aspect Ratios` is unchecked and the Scale is set to 1x.
7. Press the Play button to start the game. Press play again to terminate it.

### Notes
- Make sure your mod is reloadable during runtime by setting `"non_locking_dll_load": true` in the manifest.
- Make sure that Unity project is configured to `Reload Domain` in `When entering play mode` (Edit > Project Settings > Editor).
- Most options from `GameDebugConfigStatelessMb` won't work properly, sorry!
