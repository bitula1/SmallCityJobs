using HarmonyLib;
using Unity.Entities;
using Game.Prefabs;
using Game.Simulation;

namespace BitulaMod {
    [HarmonyPatch(typeof(UnlockSystem), "UnlockPrefab")]
    internal static class UnlockPrefabDebugPatch {
        [HarmonyPrefix]
        private static void Prefix(UnlockSystem __instance, Entity unlock, bool createEvent) {
            /*var prefabSystem = __instance.World.GetOrCreateSystemManaged<PrefabSystem>();
            var prefab = prefabSystem.GetPrefab<PrefabBase>(unlock);

            if (prefab?.name != "Roads" &&
                prefab?.name != "RoadsSmallRoads" &&
                prefab?.name != "Small Road Oneway" &&
                prefab?.name != "Pedestrian Street Small")
                return;

            Mod.log.Info($"UnlockPrefab Name={prefab.name} Type={prefab.GetType().Name} Entity={unlock} CreateEvent={createEvent}");
            Mod.log.Info(    $"UnlockPrefab Name={prefab.name} CreateEvent={createEvent}\n" +   System.Environment.StackTrace);*/
        }
    }
}