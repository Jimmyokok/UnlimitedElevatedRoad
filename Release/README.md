## Disclaimer

- This is a beta version. It may be instable or imcompatible with other mods. Please use at your own risk.

## UnlimitedElevatedRoad

- Configurable maximum pillar interval for elevated roads. The game's default interval is 80m in version 1.0.15f1. Default interval of this mod is 200m, the same as in game version 1.0.14f1. 
  - Affected road types: ordinary roads, highways, pavements, busways and tracks.

- Configurable maximum node interval for roads. The game automatically adds a node into the constructed road when its length exceeds this interval. The game's default interval is 200m. Default interval of this mod is also 200m.
  - This function is highly experimental. It affects *all* road types and even water pipes, power lines and water ways.

- No pillar switch. Once turned on, no pillars spawn when constructing elevated roads. Meanwhile, bridge properties, like special bridge pillars, cables and girders, also do not spawn.
  - Affected road types: ordinary roads, highways, pavements, busways, tracks and bridges.
  - Affected line types: power cables.

- No height limit switch. Once turned on, height limits for elevated roads are increased to 1000m.
  - Affected road types: ordinary roads, highways, pavements, busways, tracks and bridges.

## Configuring the Setting

- First launch the game with this mod loaded, and close the game (to generate the configuration file).
- Open the configuration file (..\Cities Skylines II\BepInEx\config\UnlimitedElevatedRoad.cfg), modify each configuration entries and save the file.
- Now launch the game again with this mod loaded and enjoy it!

## Requirements

- Game version 1.0.15f1.
- BepInEx 5

## Planned Features

- In-game configuration.

## Credits

- [Captain-Of-Coit](https://github.com/Captain-Of-Coit/cities-skylines-2-mod-template): A Cities: Skylines 2 mod template.
- [BepInEx](https://github.com/BepInEx/BepInEx): Unity / XNA game patcher and plugin framework.
- [Harmony](https://github.com/pardeike/Harmony): A library for patching, replacing and decorating .NET and Mono methods during runtime.
- [CSLBBS](https://www.cslbbs.net): A chinese Cities: Skylines 2 community, for extensive test and feedback efforts.
