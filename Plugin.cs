using System.Linq;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Game.Prefabs;
using Unity.Entities;
using BepInEx.Logging;
using BepInEx.Configuration;
using Unity.Collections;
using static Game.Prefabs.VehicleSelectRequirementData;
using System;
using UnityEngine.Rendering.HighDefinition;
using Colossal.Collections;
using Game.Simulation;
using Game;
using UnlimitedElevatedRoad.Systems;
using Colossal.Mathematics;
using static UnityEngine.ScriptingUtility;
using Game.Tools;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Game.UI.InGame;
using System.Reflection.Emit;
using Game.Dlc;
using static Game.Rendering.Debug.RenderPrefabRenderer;
using Game.Vehicles;

#if BEPINEX_V6
    using BepInEx.Unity.Mono;
#endif

namespace UnlimitedElevatedRoad
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, "0.2.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            Plugin.MaxElevatedLength = base.Config.Bind<int>("MaxElevatedLength", "MaxElevatedLength", 200, "最大高架长度 | Max Elevated Length");
            Plugin.MaxPillarInterval = base.Config.Bind<int>("MaxPillarInterval", "MaxPillarInterval", 200, "桥墩间隔 | Pillar Interval");
            Plugin.EnableNoPillar = base.Config.Bind<bool>("EnableNoPillar", "EnableNoPillar", false, "是否启用无桥墩 | Enable No Pillar Mode");
            Plugin.EnableUnlimitedHeight = base.Config.Bind<bool>("EnableUnlimitedHeight", "EnableUnlimitedHeight", false, "是否启用无高度限制 | Enable No Height Limit Mode");

            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        public static ConfigEntry<int> MaxElevatedLength;
        public static ConfigEntry<int> MaxPillarInterval;
        public static ConfigEntry<bool> EnableNoPillar;
        public static ConfigEntry<bool> EnableUnlimitedHeight;
    }

    
    [HarmonyPatch(typeof(NetInitializeSystem), "OnCreate")]
    public class NetInitializeSystem_OnCreatePatch
    {
        private static bool Prefix(NetInitializeSystem __instance)
        {
            __instance.World.GetOrCreateSystemManaged<PatchedNetInitializeSystem>();
            __instance.World.GetOrCreateSystemManaged<UpdateSystem>().UpdateAt<PatchedNetInitializeSystem>(SystemUpdatePhase.GameSimulation);
            return true;
        }
    }

    [HarmonyPatch(typeof(NetInitializeSystem), "OnCreateForCompiler")]
    public class NetInitializeSystem_OnCreateForCompilerPatch
    {
        private static bool Prefix(NetInitializeSystem __instance)
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(NetInitializeSystem), "OnUpdate")]
    public class NetInitializeSystem_OnUpdatePatch
    {
        private static bool Prefix(NetInitializeSystem __instance)
        {
            __instance.World.GetOrCreateSystemManaged<PatchedNetInitializeSystem>().Update();
            return false;
        }
    }
}
