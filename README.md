# Robot Repair Station — RimWorld BioTech Mod

A repair docking station for mechanoids. When a mechanoid's health drops below a
configurable threshold it will autonomously navigate to the station and undergo
automated repair using steel as a resource.

---

## Features

- Mechanoids automatically seek the nearest repair station when damaged
- Configurable health threshold (default 50 %)
- Consumes steel from nearby stockpiles during repair
- Requires electrical power
- Research required: **Mechanoid Repair Systems** (Spacer tier, after Mechanoid Basics)
- Fully compatible with all vanilla BioTech mechanoids

---

## Folder Structure

```
RobotRepairStation/
├── About/
│   └── About.xml               ← Mod metadata (name, packageId, dependencies)
├── Assemblies/
│   └── RobotRepairStation.dll  ← Compiled output (auto-generated, do not edit)
├── Defs/
│   ├── JobDefs/
│   │   └── JobDefs_RobotRepair.xml
│   ├── ThingDefs/
│   │   ├── Buildings_RobotRepairStation.xml  ← Building + ResearchDef
│   │   └── ResearchDefs.xml
│   ├── StatDefs/               ← (reserved for future custom stats)
│   └── WorkTypeDefs/           ← (reserved for future work types)
├── Languages/
│   └── English/
│       └── Keyed/
│           └── RobotRepairStation.xml  ← All translatable strings
├── Patches/
│   └── MechanoidThinkTree.xml  ← Injects repair AI node into mechanoid think tree
├── Source/
│   └── RobotRepairStation/
│       ├── RobotRepairStation.csproj
│       ├── RRS_Mod.cs                      ← Harmony bootstrap
│       ├── RRS_DefOf.cs                    ← Static JobDef references
│       ├── Building_RobotRepairStation.cs  ← Main building class
│       ├── CompRobotRepairStation.cs       ← Comp + CompProperties
│       ├── JobDriver_GoToRepairStation.cs  ← Walk-to-station job
│       ├── JobDriver_RepairAtStation.cs    ← Docked repair job
│       ├── ThinkNodes_RepairStation.cs     ← AI conditional + job giver
│       └── RepairStationTracker.cs         ← MapComponent registry
└── Textures/
    └── Things/
        └── Buildings/
            └── RobotRepairStation.png  ← 128×128 building sprite (you must add this)
```

---

## Compiling the C# Code

### Prerequisites
- .NET SDK 6+ (or Visual Studio 2022)
- RimWorld installed via Steam

### Steps

1. Open `Source/RobotRepairStation/RobotRepairStation.csproj` in Visual Studio or Rider.
2. Set the `RimWorldPath` property in the `.csproj` to your RimWorld install directory,
   or set the `RimWorldPath` environment variable.
3. Build → Release.
4. The compiled `RobotRepairStation.dll` is automatically placed in `Assemblies/`.

### Command-line build
```bash
cd Source/RobotRepairStation
dotnet build -c Release
```

---

## Adding the Texture

Place a **128 × 128 px** PNG at:
```
Textures/Things/Buildings/RobotRepairStation.png
```

Recommended style: match vanilla BioTech buildings — dark metal tones with blue/teal
accent lighting. The building occupies a 2×2 tile footprint.

---

## Installing the Mod

1. Copy the entire `RobotRepairStation` folder to:
   - **Windows:** `%APPDATA%\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods\`
   - **Linux:** `~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Mods/`
   - **macOS:** `~/Library/Application Support/RimWorld/Mods/`
2. Enable the mod in-game and make sure **Biotech DLC** is active.

---

## Configuration Reference (ThingDef XML)

All values can be tweaked directly in `Defs/ThingDefs/Buildings_RobotRepairStation.xml`
inside the `<li Class="RobotRepairStation.CompProperties_RobotRepairStation">` block:

| Property               | Default | Description                                       |
|------------------------|---------|---------------------------------------------------|
| `repairHealthThreshold`| `0.5`   | Health fraction below which mechanoids seek repair|
| `repairSpeedPerTick`   | `0.0005`| Health restored per game tick while docked        |
| `steelPerRepairCycle`  | `1`     | Steel consumed per repair interval                |
| `repairTickInterval`   | `500`   | Ticks between each steel consumption cycle        |
| `maxRepairRange`       | `30`    | Max cell distance for mechanoids to detect station|

---

## License

MIT — free to use, modify, and distribute with attribution.
