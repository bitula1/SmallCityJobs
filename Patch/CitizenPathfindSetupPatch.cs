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
using Game.Prefabs;
using Game.Economy;
using Game.Buildings;
using Game;

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

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentTypeHandle<ServiceAvailable>> ServiceAvailableType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentTypeHandle<ServiceAvailable>>("m_ServiceAvailableType");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentTypeHandle<PrefabRef>> PrefabType =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentTypeHandle<PrefabRef>>("m_PrefabRefType");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<IndustrialProcessData>> IndustrialProcessDatas =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<IndustrialProcessData>>("m_IndustrialProcessDatas");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<LeisureProviderData>> LeisureProviderDatas =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<LeisureProviderData>>("m_LeisureProviderDatas");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<ResourceData>> ResourceDatas =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<ResourceData>>("m_ResourceDatas");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<ServiceCompanyData>> ServiceDatas =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<ServiceCompanyData>>("m_ServiceDatas");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, ComponentLookup<Building>> BuildingDatas =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, ComponentLookup<Building>>("m_BuildingDatas");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, BufferLookup<Game.Economy.Resources>> ResourcesLookup =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, BufferLookup<Game.Economy.Resources>>("m_Resources");

        private static readonly AccessTools.StructFieldRef<CitizenPathfindSetup, EntityQuery> LeisureProviderQuery =
            AccessTools.StructFieldRefAccess<CitizenPathfindSetup, EntityQuery>("m_LeisureProviderQuery");

        private static readonly MethodInfo GetComponentLookupMethod = AccessTools.Method(typeof(SystemBase), "GetComponentLookup",
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

        private static ComponentLookup<T> GetComponentLookup<T>(PathfindSetupSystem system, bool isReadOnly) where T : unmanaged, IComponentData {
            MethodInfo genericMethod =
                GetComponentLookupMethod.MakeGenericMethod(typeof(T));

            return (ComponentLookup<T>)genericMethod.Invoke(
                system,
                new object[] { isReadOnly });
        }

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            MethodInfo originalJobSeeker = AccessTools.Method(
                typeof(CitizenPathfindSetup),
                nameof(CitizenPathfindSetup.SetupJobSeekerTo),
                new Type[] {
                    typeof(PathfindSetupSystem),
                    typeof(PathfindSetupSystem.SetupData),
                    typeof(JobHandle)
            });

            MethodInfo replacementJobSeeker = AccessTools.Method(
                typeof(PathfindSetupSystemPatch),
                nameof(SetupJobSeekerToBridge));

            MethodInfo originalLeisure = AccessTools.Method(
                typeof(CitizenPathfindSetup),
                nameof(CitizenPathfindSetup.SetupLeisureTarget),
                new Type[] {
                typeof(PathfindSetupSystem),
                typeof(PathfindSetupSystem.SetupData),
                typeof(JobHandle)
                });

            MethodInfo replacementLeisure = AccessTools.Method(
                typeof(PathfindSetupSystemPatch),
                nameof(SetupLeisureTargetBridge));

            int jobSeekerReplaced = 0;
            int leisureReplaced = 0;

            for (int i = 0; i < codes.Count; i++) {

                if (codes[i].opcode != OpCodes.Call ||
                    codes[i].operand is not MethodInfo method)
                    continue;

                if (method == originalJobSeeker) {
                    codes[i].operand = replacementJobSeeker;
                    jobSeekerReplaced++;
                } else if (method == originalLeisure) {
                    codes[i].operand = replacementLeisure;
                    leisureReplaced++;
                }
            }

            Mod.log.Info(
                $"Harmony replaced {jobSeekerReplaced} SetupJobSeekerTo call(s), " +
                $"{leisureReplaced} SetupLeisureTarget call(s)");

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

            ComponentLookup<SmallCityJobsComponent> smallCitySearch = GetComponentLookup<SmallCityJobsComponent>(system, true);

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

        public static JobHandle SetupLeisureTargetBridge(  ref CitizenPathfindSetup setup, PathfindSetupSystem system, PathfindSetupSystem.SetupData setupData, JobHandle inputDeps) {

            ref EntityTypeHandle entityType =
                ref EntityType(ref setup);

            ref ComponentTypeHandle<ServiceAvailable> serviceAvailableType =
                ref ServiceAvailableType(ref setup);

            ref ComponentTypeHandle<PrefabRef> prefabType =
                ref PrefabType(ref setup);

            ref ComponentLookup<IndustrialProcessData> industrialProcessDatas =
                ref IndustrialProcessDatas(ref setup);

            ref ComponentLookup<LeisureProviderData> leisureProviderDatas =
                ref LeisureProviderDatas(ref setup);

            ref ComponentLookup<ResourceData> resourceDatas =
                ref ResourceDatas(ref setup);

            ref ComponentLookup<ServiceCompanyData> serviceDatas =
                ref ServiceDatas(ref setup);

            ref ComponentLookup<Building> buildingDatas =
                ref BuildingDatas(ref setup);

            ref BufferLookup<Game.Economy.Resources> resources =
                ref ResourcesLookup(ref setup);

            ref EntityQuery leisureProviderQuery =
                ref LeisureProviderQuery(ref setup);

            ResourceSystem resourceSystem =
                system.World.GetOrCreateSystemManaged<ResourceSystem>();

            LeisureSystem leisureSystem =
                system.World.GetOrCreateSystemManaged<LeisureSystem>();


            entityType.Update(system);
            serviceAvailableType.Update(system);
            prefabType.Update(system);

            industrialProcessDatas.Update(system);
            leisureProviderDatas.Update(system);
            resourceDatas.Update(system);
            serviceDatas.Update(system);
            buildingDatas.Update(system);
            resources.Update(system);
            EndFrameBarrier endFrameBarrier = system.World.GetOrCreateSystemManaged<EndFrameBarrier>();
            EntityCommandBuffer.ParallelWriter commandBuffer = endFrameBarrier.CreateCommandBuffer().AsParallelWriter();
            SmallCityJobs smallCityJobs = SmallCityJobs.Create(ref system.CheckedStateRef, commandBuffer);

            JobHandle handle = new SetupLeisureTargetJob {
                m_EntityType = entityType,
                m_ServiceAvailableType = serviceAvailableType,
                m_PrefabType = prefabType,

                m_IndustrialProcessDatas = industrialProcessDatas,
                m_LeisureProviderDatas = leisureProviderDatas,
                m_ResourceDatas = resourceDatas,
                m_ServiceDatas = serviceDatas,
                m_BuildingDatas = buildingDatas,
                m_Resources = resources,

                m_SetupData = setupData,
                m_SmallCityJobs = smallCityJobs,
                m_ResourcePrefabs = resourceSystem.GetPrefabs(),
                m_LeisureSystemUpdateInterval = leisureSystem.GetUpdateInterval(SystemUpdatePhase.GameSimulation),
                m_PropertyRenters = GetComponentLookup<PropertyRenter>(system, true),
                m_CompanyDatas = GetComponentLookup<CompanyData>(system, true),

            }.ScheduleParallel(leisureProviderQuery, inputDeps);
            resourceSystem.AddPrefabsReader(handle);
            SmallCityJobs.AddProducer(ref system.CheckedStateRef, handle);
            endFrameBarrier.AddJobHandleForProducer(handle);
            return handle;
        }
    }
}