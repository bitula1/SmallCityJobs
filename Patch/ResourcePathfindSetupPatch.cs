using BitulaMod.SmallCity;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Vehicles;
using Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;
using Unity.Jobs;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Game.Pathfind;

namespace BitulaMod.Patch {
    [HarmonyPatch]
    internal class ResourcePathfindSetupPatch {
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

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            List<CodeInstruction> codes = new List<CodeInstruction>(instructions);

            MethodInfo originalResourceSeller = AccessTools.Method(
                typeof(ResourcePathfindSetup),
                nameof(ResourcePathfindSetup.SetupResourceSeller),
                new Type[] {
            typeof(PathfindSetupSystem),
            typeof(PathfindSetupSystem.SetupData),
            typeof(JobHandle)
                });

            MethodInfo replacementResourceSeller = AccessTools.Method(
                typeof(ResourcePathfindSetupPatch),
                nameof(SetupResourceSellerBridge));

            int resourceSellerReplaced = 0;

            for (int i = 0; i < codes.Count; i++) {
                if (codes[i].opcode != OpCodes.Call ||
                    codes[i].operand is not MethodInfo method)
                    continue;

                if (method == originalResourceSeller) {
                    codes[i].operand = replacementResourceSeller;
                    resourceSellerReplaced++;
                }
            }

            Mod.log.Info($"Harmony replaced {resourceSellerReplaced} SetupResourceSeller call(s)");

            return codes;
        }

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, EntityTypeHandle> EntityType =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, EntityTypeHandle>("m_EntityType");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferLookup<Game.Economy.Resources>> ResourcesLookup =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferLookup<Game.Economy.Resources>>("m_Resources");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<IndustrialProcessData>> IndustrialProcessDatas =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<IndustrialProcessData>>("m_IndustrialProcessDatas");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<Game.Buildings.CargoTransportStation>> CargoTransportStations =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<Game.Buildings.CargoTransportStation>>("m_CargoTransportStations");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<StorageCompanyData>> StorageCompanyDatas =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<StorageCompanyData>>("m_StorageCompanyDatas");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<PropertyRenter>> PropertyRenters =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<PropertyRenter>>("m_PropertyRenters");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferLookup<TradeCost>> TradeCosts =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferLookup<TradeCost>>("m_TradeCosts");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<ServiceAvailable>> ServiceAvailables =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<ServiceAvailable>>("m_ServiceAvailables");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<ServiceCompanyData>> ServiceCompanies =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<ServiceCompanyData>>("m_ServiceCompanies");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<ResourceData>> ResourceDatas =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<ResourceData>>("m_ResourceDatas");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<Game.Objects.OutsideConnection>> OutsideConnections =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<Game.Objects.OutsideConnection>>("m_OutsideConnections");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferTypeHandle<StorageTransferRequest>> StorageTransferRequestType =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferTypeHandle<StorageTransferRequest>>("m_StorageTransferRequestType");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferTypeHandle<TripNeeded>> TripNeededType =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferTypeHandle<TripNeeded>>("m_TripNeededType");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<PrefabRef>> Prefabs =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<PrefabRef>>("m_Prefabs");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<Building>> Buildings =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<Building>>("m_Buildings");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<Game.Vehicles.DeliveryTruck>> DeliveryTrucks =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<Game.Vehicles.DeliveryTruck>>("m_DeliveryTrucks");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferLookup<GuestVehicle>> GuestVehicleBufs =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferLookup<GuestVehicle>>("m_GuestVehicleBufs");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferLookup<LayoutElement>> LayoutElementBufs =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferLookup<LayoutElement>>("m_LayoutElementBufs");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferTypeHandle<OwnedVehicle>> OwnedVehicleType =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferTypeHandle<OwnedVehicle>>("m_OwnedVehicleType");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, ComponentLookup<TransportCompanyData>> TransportCompanyData =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, ComponentLookup<TransportCompanyData>>("m_TransportCompanyData");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, BufferTypeHandle<InstalledUpgrade>> InstalledUpgradeType =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, BufferTypeHandle<InstalledUpgrade>>("m_InstalledUpgradeType");

        private static readonly AccessTools.StructFieldRef<ResourcePathfindSetup, EntityQuery> ResourceSellerQuery =
            AccessTools.StructFieldRefAccess<ResourcePathfindSetup, EntityQuery>("m_ResourceSellerQuery");

        public static JobHandle SetupResourceSellerBridge(  ref ResourcePathfindSetup setup,  PathfindSetupSystem system,  PathfindSetupSystem.SetupData setupData,
            JobHandle inputDeps) {

            ref EntityTypeHandle entityType =
                ref EntityType(ref setup);

            ref BufferLookup<Game.Economy.Resources> resources =
                ref ResourcesLookup(ref setup);

            ref ComponentLookup<IndustrialProcessData> industrialProcessDatas =
                ref IndustrialProcessDatas(ref setup);

            ref ComponentLookup<Game.Buildings.CargoTransportStation> cargoTransportStations =
                ref CargoTransportStations(ref setup);

            ref ComponentLookup<StorageCompanyData> storageCompanyDatas =
                ref StorageCompanyDatas(ref setup);

            ref ComponentLookup<PropertyRenter> propertyRenters =
                ref PropertyRenters(ref setup);

            ref BufferLookup<TradeCost> tradeCosts =
                ref TradeCosts(ref setup);

            ref ComponentLookup<ServiceAvailable> serviceAvailables =
                ref ServiceAvailables(ref setup);

            ref ComponentLookup<ServiceCompanyData> serviceCompanies =
                ref ServiceCompanies(ref setup);

            ref ComponentLookup<ResourceData> resourceDatas =
                ref ResourceDatas(ref setup);

            ref ComponentLookup<Game.Objects.OutsideConnection> outsideConnections =
                ref OutsideConnections(ref setup);

            ref BufferTypeHandle<StorageTransferRequest> storageTransferRequestType =
                ref StorageTransferRequestType(ref setup);

            ref BufferTypeHandle<TripNeeded> tripNeededType =
                ref TripNeededType(ref setup);

            ref ComponentLookup<PrefabRef> prefabs =
                ref Prefabs(ref setup);

            ref ComponentLookup<Building> buildings =
                ref Buildings(ref setup);

            ref ComponentLookup<Game.Vehicles.DeliveryTruck> deliveryTrucks =
                ref DeliveryTrucks(ref setup);

            ref BufferLookup<GuestVehicle> guestVehicleBufs =
                ref GuestVehicleBufs(ref setup);

            ref BufferLookup<LayoutElement> layoutElementBufs =
                ref LayoutElementBufs(ref setup);

            ref BufferTypeHandle<OwnedVehicle> ownedVehicleType =
                ref OwnedVehicleType(ref setup);

            ref ComponentLookup<TransportCompanyData> transportCompanyData =
                ref TransportCompanyData(ref setup);

            ref BufferTypeHandle<InstalledUpgrade> installedUpgradeType =
                ref InstalledUpgradeType(ref setup);

            ref EntityQuery resourceSellerQuery =
                ref ResourceSellerQuery(ref setup);

            ResourceSystem resourceSystem =
                system.World.GetOrCreateSystemManaged<ResourceSystem>();

            entityType.Update(system);
            resources.Update(system);
            industrialProcessDatas.Update(system);
            cargoTransportStations.Update(system);
            storageCompanyDatas.Update(system);
            propertyRenters.Update(system);
            tradeCosts.Update(system);
            serviceAvailables.Update(system);
            serviceCompanies.Update(system);
            resourceDatas.Update(system);
            outsideConnections.Update(system);
            storageTransferRequestType.Update(system);
            tripNeededType.Update(system);
            prefabs.Update(system);
            buildings.Update(system);
            deliveryTrucks.Update(system);
            guestVehicleBufs.Update(system);
            layoutElementBufs.Update(system);
            ownedVehicleType.Update(system);
            transportCompanyData.Update(system);
            installedUpgradeType.Update(system);

            EndFrameBarrier endFrameBarrier =
                system.World.GetOrCreateSystemManaged<EndFrameBarrier>();

            EntityCommandBuffer.ParallelWriter commandBuffer =
                endFrameBarrier.CreateCommandBuffer().AsParallelWriter();

            SmallCity<SCShopping> smallCityShopping =
                SmallCity<SCShopping>.Create(
                    ref system.CheckedStateRef,
                    commandBuffer);

            JobHandle handle = new SetupResourceSellerJob {
                m_EntityType = entityType,
                m_OwnedVehicles = ownedVehicleType,
                m_StorageTransferRequestType = storageTransferRequestType,
                m_TripNeededType = tripNeededType,
                m_Resources = resources,
                m_IndustrialProcessDatas = industrialProcessDatas,
                m_CargoTransportStations = cargoTransportStations,
                m_StorageCompanyDatas = storageCompanyDatas,
                m_TransportCompanyDatas = transportCompanyData,
                m_PropertyRenters = propertyRenters,
                m_TradeCosts = tradeCosts,
                m_ServiceAvailables = serviceAvailables,
                m_ServiceCompanies = serviceCompanies,
                m_ResourceDatas = resourceDatas,
                m_ResourcePrefabs = resourceSystem.GetPrefabs(),
                m_OutsideConnections = outsideConnections,
                m_Prefabs = prefabs,
                m_Buildings = buildings,
                m_DeliveryTrucks = deliveryTrucks,
                m_GuestVehicleBufs = guestVehicleBufs,
                m_LayoutElementBufs = layoutElementBufs,
                m_InstalledUpgradeType = installedUpgradeType,
                m_RandomSeed = RandomSeed.Next(),
                m_SetupData = setupData,

                m_CommandBuffer = commandBuffer,
                m_SmallCityShopping = smallCityShopping

            }.ScheduleParallel(resourceSellerQuery, inputDeps);

            resourceSystem.AddPrefabsReader(handle);
            SmallCity<SCShopping>.AddProducer(ref system.CheckedStateRef, handle);
            endFrameBarrier.AddJobHandleForProducer(handle);

            return handle;
        }
    }
}
