using HarmonyLib;
using Game.Prefabs;
using Unity.Entities;
using BitulaMod;

[HarmonyPatch(typeof(UnlockSystem), "UnlockPrefab")]
internal static class UnlockPrefabDebugPatch {

    [HarmonyPrefix]
    private static void Prefix(
        UnlockSystem __instance,
        Entity unlock,
        bool createEvent) {

        var prefabSystem =
            __instance.World.GetOrCreateSystemManaged<PrefabSystem>();

        var prefab = prefabSystem.GetPrefab<PrefabBase>(unlock);

        if (prefab?.name != "Water & Sewage")
            return;

        Mod.log.Info(
            $"UnlockPrefab Name={prefab.name} " +
            $"Type={prefab.GetType().Name} " +
            $"Entity={unlock} " +
            $"CreateEvent={createEvent}");
    }
}