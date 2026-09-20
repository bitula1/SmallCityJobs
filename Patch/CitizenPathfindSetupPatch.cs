using HarmonyLib;
using Game.Simulation;
using Game.Pathfind;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Unity.Jobs;
using System;
using Unity.Entities;
using Game.City;
using Game.Companies;

namespace BitulaMod {
    [HarmonyPatch]
    public static class PathfindSetupSystemPatch {
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, EntityTypeHandle> EntityType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, EntityTypeHandle>("m_EntityType");
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentTypeHandle<FreeWorkplaces>> FreeWorkplaceType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentTypeHandle<FreeWorkplaces>>("m_FreeWorkplaceType");
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentTypeHandle<WorkProvider>> WorkProviderType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentTypeHandle<WorkProvider>>("m_WorkProviderType");
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentTypeHandle<CityServiceUpkeep>> CityServiceType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentTypeHandle<CityServiceUpkeep>>("m_CityServiceType");
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<Game.Objects.OutsideConnection>> OutsideConnections =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<Game.Objects.OutsideConnection>>("m_OutsideConnections");
        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, EntityQuery> FreeWorkplaceQuery =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, EntityQuery>("m_FreeWorkplaceQuery");

        private static readonly MethodInfo GetComponentLookupMethod =  AccessTools.Method( typeof(SystemBase), "GetComponentLookup",  
            new Type[] { typeof(bool) });
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod() {
            return AccessTools.Method(
                typeof(PathfindSetupSystem),
                "FindTargets",
                new Type[] {
            typeof(SetupTargetType),
            typeof(PathfindSetupSystem.SetupData).MakeByRefType()
                });
        }

        private static ComponentLookup<T> GetComponentLookup<T>( PathfindSetupSystem system, bool isReadOnly) where T : unmanaged, IComponentData {
            MethodInfo genericMethod =
                GetComponentLookupMethod.MakeGenericMethod(typeof(T));

            return (ComponentLookup<T>)genericMethod.Invoke(
                system,
                new object[] { isReadOnly });
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            MethodInfo original = AccessTools.Method(
                typeof(CitizenPathfindSetup),
                nameof(CitizenPathfindSetup.SetupJobSeekerTo),
                new Type[] {
                    typeof(PathfindSetupSystem),
                    typeof(PathfindSetupSystem.SetupData),
                    typeof(JobHandle)
                });

            MethodInfo replacement = AccessTools.Method(
                typeof(PathfindSetupSystemPatch),
                nameof(SetupJobSeekerToBridge));

            int replaced = 0;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode == OpCodes.Call &&
                    codes[i].operand is MethodInfo method &&
                    method == original) {

                    codes[i].operand = replacement;
                    replaced++;
                }
            }

            Mod.log.Info($"Harmony replaced {replaced} SetupJobSeekerTo call(s)");

            return codes;
        }

        public static JobHandle SetupJobSeekerToBridge(
           ref CitizenPathfindSetup setup,
           PathfindSetupSystem system,
           PathfindSetupSystem.SetupData setupData,
           JobHandle inputDeps) {

            ref EntityTypeHandle entityType = ref EntityType(ref setup);
            ref ComponentTypeHandle<FreeWorkplaces> freeWorkplaceType = ref FreeWorkplaceType(ref setup);
            ref ComponentTypeHandle<WorkProvider> workProviderType = ref WorkProviderType(ref setup);
            ref ComponentTypeHandle<CityServiceUpkeep> cityServiceType = ref CityServiceType(ref setup);
            ref ComponentLookup<Game.Objects.OutsideConnection> outsideConnections = ref OutsideConnections(ref setup);
            ref EntityQuery freeWorkplaceQuery = ref FreeWorkplaceQuery(ref setup);

            entityType.Update(system);
            freeWorkplaceType.Update(system);
            workProviderType.Update(system);
            cityServiceType.Update(system);
            outsideConnections.Update(system);

            ComponentLookup<SmallCityJobsComponent> smallCitySearch =  GetComponentLookup<SmallCityJobsComponent>(system, true);

            return new SetupJobSeekerToJob {
                m_EntityType = entityType,
                m_FreeWorkplaceType = freeWorkplaceType,
                m_WorkProviderType = workProviderType,
                m_CityServiceType = cityServiceType,
                m_OutsideConnections = outsideConnections,
                m_SetupData = setupData,
                m_SmallCitySearch = smallCitySearch,
            }.ScheduleParallel(freeWorkplaceQuery, inputDeps);
        }
    }
    }