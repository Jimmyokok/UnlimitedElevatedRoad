using ConfigurableElevatedRoad.Systems;
using HarmonyLib;

namespace ConfigurableElevatedRoad.Patches
{
    /*
    [HarmonyPatch]
    class ConfigurableElevatedRoadPatches
    {
        [HarmonyPatch(typeof(Game.Prefabs.NetInitializeSystem), "OnUpdate")]
        [HarmonyPostfix]
        public static void NetInitializeSystem_OnUpdate(Game.Prefabs.NetInitializeSystem __instance)
        {
            __instance.World.GetOrCreateSystemManaged<NetCompositionDataFixSystem>().Update();
        }
    }
    */
}
