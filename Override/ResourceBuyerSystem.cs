using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Debug;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Reflection;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;
using BitulaMod.SmallCity;

namespace BitulaMod
{
	// Token: 0x020015EA RID: 5610
	public partial class ResourceBuyerSystem : GameSystemBase
	{
		// Token: 0x06006B7B RID: 27515 RVA: 0x0035D8E5 File Offset: 0x0035BAE5
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x06006B7C RID: 27516 RVA: 0x003AC4C4 File Offset: 0x003AA6C4
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
			this.m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			this.m_PersonalCarSelectData = new PersonalCarSelectData(this);
			this.m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
			this.m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_SalesQueue = new NativeQueue<ResourceBuyerSystem.SalesEvent>(Allocator.Persistent);
			this.m_BuyerOutcomes = new NativeArray<int>(12, Allocator.Persistent, NativeArrayOptions.ClearMemory);
			this.m_FailedShoppingOrigins = new NativeArray<int>(6, Allocator.Persistent, NativeArrayOptions.ClearMemory);
			this.m_FailedShoppingResources = new NativeArray<int>(42, Allocator.Persistent, NativeArrayOptions.ClearMemory);
			this.m_BuyerQuery = base.GetEntityQuery(new EntityQueryDesc[]
			{
				new EntityQueryDesc
				{
					All = new ComponentType[]
					{
						ComponentType.ReadWrite<ResourceBuyer>(),
						ComponentType.ReadWrite<TripNeeded>()
					},
					None = new ComponentType[]
					{
						ComponentType.ReadOnly<TravelPurpose>(),
						ComponentType.ReadOnly<Deleted>(),
						ComponentType.ReadOnly<Temp>()
					}
				},
				new EntityQueryDesc
				{
					All = new ComponentType[] { ComponentType.ReadOnly<ResourceBought>() },
					None = new ComponentType[]
					{
						ComponentType.ReadOnly<Deleted>(),
						ComponentType.ReadOnly<Temp>()
					}
				}
			});
			this.m_CarPrefabQuery = base.GetEntityQuery(new EntityQueryDesc[] { PersonalCarSelectData.GetEntityQueryDesc() });
			this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_PopulationQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<Population>() });
			this.m_ResidentPrefabQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<ObjectData>(),
				ComponentType.ReadOnly<HumanData>(),
				ComponentType.ReadOnly<ResidentData>(),
				ComponentType.ReadOnly<PrefabData>()
			});
			this.m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(), ComponentType.ReadWrite<PathElement>());
			this.m_TripPriorityParametersQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TripPriorityParametersData>() });
			base.RequireForUpdate(this.m_BuyerQuery);
			base.RequireForUpdate(this.m_EconomyParameterQuery);
			base.RequireForUpdate(this.m_PopulationQuery);
			base.RequireForUpdate(this.m_TripPriorityParametersQuery);
		}

		// Token: 0x06006B7D RID: 27517 RVA: 0x003AC76A File Offset: 0x003AA96A
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_SalesQueue.Dispose();
			this.m_BuyerOutcomes.Dispose();
			this.m_FailedShoppingOrigins.Dispose();
			this.m_FailedShoppingResources.Dispose();
			base.OnDestroy();
		}

		// Token: 0x06006B7E RID: 27518 RVA: 0x00379EAD File Offset: 0x003780AD
		[Preserve]
		protected override void OnStopRunning()
		{
			base.OnStopRunning();
		}

		// Token: 0x06006B7F RID: 27519 RVA: 0x003AC7A0 File Offset: 0x003AA9A0
		[Preserve]
		protected override void OnUpdate()
		{
			if (this.m_BuyerQuery.CalculateEntityCount() > 0)
			{
				JobHandle jobHandle;
				this.m_PersonalCarSelectData.PreUpdate(this, this.m_CityConfigurationSystem, this.m_CarPrefabQuery, Allocator.TempJob, out jobHandle);
				ResourceBuyerSystem.HandleBuyersJob handleBuyersJob = default(ResourceBuyerSystem.HandleBuyersJob);
				handleBuyersJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_BuyerType = InternalCompilerInterface.GetComponentTypeHandle<ResourceBuyer>(ref this.__TypeHandle.__Game_Companies_ResourceBuyer_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_BoughtType = InternalCompilerInterface.GetComponentTypeHandle<ResourceBought>(ref this.__TypeHandle.__Game_Citizens_ResourceBought_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_TripType = InternalCompilerInterface.GetBufferTypeHandle<TripNeeded>(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_CreatureDataType = InternalCompilerInterface.GetComponentTypeHandle<CreatureData>(ref this.__TypeHandle.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_ResidentDataType = InternalCompilerInterface.GetComponentTypeHandle<ResidentData>(ref this.__TypeHandle.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_AttendingMeetingType = InternalCompilerInterface.GetComponentTypeHandle<AttendingMeeting>(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				handleBuyersJob.m_ServiceAvailables = InternalCompilerInterface.GetComponentLookup<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_PathInformation = InternalCompilerInterface.GetComponentLookup<PathInformation>(ref this.__TypeHandle.__Game_Pathfind_PathInformation_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Properties = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_CarKeepers = InternalCompilerInterface.GetComponentLookup<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_BicycleOwners = InternalCompilerInterface.GetComponentLookup<BicycleOwner>(ref this.__TypeHandle.__Game_Citizens_BicycleOwner_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_ParkedCarData = InternalCompilerInterface.GetComponentLookup<ParkedCar>(ref this.__TypeHandle.__Game_Vehicles_ParkedCar_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_PersonalCarData = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Targets = InternalCompilerInterface.GetComponentLookup<Target>(ref this.__TypeHandle.__Game_Common_Target_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_CurrentBuildings = InternalCompilerInterface.GetComponentLookup<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_CommuterHouseholds = InternalCompilerInterface.GetComponentLookup<CommuterHousehold>(ref this.__TypeHandle.__Game_Citizens_CommuterHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_DeliveryTrucks = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.DeliveryTruck>(ref this.__TypeHandle.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_StorageCompanies = InternalCompilerInterface.GetComponentLookup<Game.Companies.StorageCompany>(ref this.__TypeHandle.__Game_Companies_StorageCompany_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_GuestVehicles = InternalCompilerInterface.GetBufferLookup<GuestVehicle>(ref this.__TypeHandle.__Game_Vehicles_GuestVehicle_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_LayoutElements = InternalCompilerInterface.GetBufferLookup<LayoutElement>(ref this.__TypeHandle.__Game_Vehicles_LayoutElement_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
				handleBuyersJob.m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_PrefabRefData = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_PrefabCarData = InternalCompilerInterface.GetComponentLookup<CarData>(ref this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_ObjectGeometryData = InternalCompilerInterface.GetComponentLookup<ObjectGeometryData>(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_PrefabHumanData = InternalCompilerInterface.GetComponentLookup<HumanData>(ref this.__TypeHandle.__Game_Prefabs_HumanData_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_CoordinatedMeetings = InternalCompilerInterface.GetComponentLookup<CoordinatedMeeting>(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_HaveCoordinatedMeetingDatas = InternalCompilerInterface.GetBufferLookup<HaveCoordinatedMeetingData>(ref this.__TypeHandle.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_OutsideConnectionDatas = InternalCompilerInterface.GetComponentLookup<OutsideConnectionData>(ref this.__TypeHandle.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_Populations = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RW_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_CurrentDistrictData = InternalCompilerInterface.GetComponentLookup<CurrentDistrict>(ref this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_DistrictModifiers = InternalCompilerInterface.GetBufferLookup<DistrictModifier>(ref this.__TypeHandle.__Game_Areas_DistrictModifier_RO_BufferLookup, ref base.CheckedStateRef);
				handleBuyersJob.m_TimeOfDay = this.m_TimeSystem.normalizedTime;
				handleBuyersJob.m_FrameIndex = this.m_SimulationSystem.frameIndex;
				handleBuyersJob.m_RandomSeed = RandomSeed.Next();
				handleBuyersJob.m_PathfindTypes = this.m_PathfindTypes;
				JobHandle jobHandle2;
				handleBuyersJob.m_HumanChunks = this.m_ResidentPrefabQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator, out jobHandle2);
				handleBuyersJob.m_PersonalCarSelectData = this.m_PersonalCarSelectData;
				handleBuyersJob.m_PathfindQueue = this.m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter();
				handleBuyersJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
				handleBuyersJob.m_EconomyParameterData = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
				handleBuyersJob.m_TripPriorityParameters = this.m_TripPriorityParametersQuery.GetSingleton<TripPriorityParametersData>();
				handleBuyersJob.m_City = this.m_CitySystem.City;
				handleBuyersJob.m_SalesQueue = this.m_SalesQueue.AsParallelWriter();
				handleBuyersJob.m_BuyerOutcomes = this.m_BuyerOutcomes;
				handleBuyersJob.m_FailedShoppingOrigins = this.m_FailedShoppingOrigins;
				handleBuyersJob.m_FailedShoppingResources = this.m_FailedShoppingResources;
                handleBuyersJob.m_SmallCityShopping = SmallCity<SCShopping>.Create(ref base.CheckedStateRef, handleBuyersJob.m_CommandBuffer);
                ResourceBuyerSystem.HandleBuyersJob handleBuyersJob2 = handleBuyersJob;
				base.Dependency = handleBuyersJob2.ScheduleParallel(this.m_BuyerQuery, JobHandle.CombineDependencies(base.Dependency, jobHandle2, jobHandle));
                SmallCity<SCShopping>.AddProducer(ref base.CheckedStateRef, base.Dependency);
                this.m_ResourceSystem.AddPrefabsReader(base.Dependency);
				this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
				this.m_PathfindSetupSystem.AddQueueWriter(base.Dependency);
				this.m_WriteDeps = JobHandle.CombineDependencies(base.Dependency, this.m_WriteDeps);
				ResourceBuyerSystem.BuyJob buyJob = default(ResourceBuyerSystem.BuyJob);
				buyJob.m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferLookup, ref base.CheckedStateRef);
				buyJob.m_SalesQueue = this.m_SalesQueue;
				buyJob.m_Services = InternalCompilerInterface.GetComponentLookup<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RW_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_TransformDatas = InternalCompilerInterface.GetComponentLookup<Game.Objects.Transform>(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
				buyJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
				buyJob.m_HouseholdAnimals = InternalCompilerInterface.GetBufferLookup<HouseholdAnimal>(ref this.__TypeHandle.__Game_Citizens_HouseholdAnimal_RO_BufferLookup, ref base.CheckedStateRef);
				buyJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_ServiceCompanies = InternalCompilerInterface.GetComponentLookup<ServiceCompanyData>(ref this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_Storages = InternalCompilerInterface.GetComponentLookup<Game.Companies.StorageCompany>(ref this.__TypeHandle.__Game_Companies_StorageCompany_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RW_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_BuyingCompanies = InternalCompilerInterface.GetComponentLookup<BuyingCompany>(ref this.__TypeHandle.__Game_Companies_BuyingCompany_RW_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_TradeCosts = InternalCompilerInterface.GetBufferLookup<TradeCost>(ref this.__TypeHandle.__Game_Companies_TradeCost_RW_BufferLookup, ref base.CheckedStateRef);
				buyJob.m_CompanyStatistics = InternalCompilerInterface.GetComponentLookup<CompanyStatisticData>(ref this.__TypeHandle.__Game_Companies_CompanyStatisticData_RW_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
				buyJob.m_RandomSeed = RandomSeed.Next();
				buyJob.m_FrameIndex = this.m_SimulationSystem.frameIndex;
				buyJob.m_PersonalCarSelectData = this.m_PersonalCarSelectData;
				buyJob.m_PopulationData = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
				buyJob.m_PopulationEntity = this.m_PopulationQuery.GetSingletonEntity();
                buyJob.m_SmallCityShopping = SmallCity<SCShopping>.Create( ref base.CheckedStateRef, buyJob.m_CommandBuffer.AsParallelWriter());
                JobHandle jobHandle3;
				buyJob.m_CitizenConsumptionAccumulator = this.m_CityProductionStatisticSystem.GetCityResourceUsageAccumulator(CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, out jobHandle3);
				buyJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer();
				ResourceBuyerSystem.BuyJob buyJob2 = buyJob;
				base.Dependency = buyJob2.Schedule(JobHandle.CombineDependencies(base.Dependency, jobHandle3));
                SmallCity<SCShopping>.AddProducer(ref base.CheckedStateRef, base.Dependency);
                this.m_PersonalCarSelectData.PostUpdate(base.Dependency);
				this.m_ResourceSystem.AddPrefabsReader(base.Dependency);
				this.m_TaxSystem.AddReader(base.Dependency);
				this.m_CityProductionStatisticSystem.AddCityUsageAccumulatorWriter(CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, base.Dependency);
				this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			}
		}

		// Token: 0x06006B80 RID: 27520 RVA: 0x003AD10C File Offset: 0x003AB30C
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x06006B81 RID: 27521 RVA: 0x003AD12D File Offset: 0x003AB32D
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x06006B82 RID: 27522 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public ResourceBuyerSystem()
		{
		}

		// Token: 0x04009F6B RID: 40811
		private const int UPDATE_INTERVAL = 16;

		// Token: 0x04009F6C RID: 40812
		private EntityQuery m_BuyerQuery;

		// Token: 0x04009F6D RID: 40813
		private EntityQuery m_CarPrefabQuery;

		// Token: 0x04009F6E RID: 40814
		private EntityQuery m_EconomyParameterQuery;

		// Token: 0x04009F6F RID: 40815
		private EntityQuery m_ResidentPrefabQuery;

		// Token: 0x04009F70 RID: 40816
		private EntityQuery m_PopulationQuery;

		// Token: 0x04009F71 RID: 40817
		private EntityQuery m_TripPriorityParametersQuery;

		// Token: 0x04009F72 RID: 40818
		private ComponentTypeSet m_PathfindTypes;

		// Token: 0x04009F73 RID: 40819
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x04009F74 RID: 40820
		private PathfindSetupSystem m_PathfindSetupSystem;

		// Token: 0x04009F75 RID: 40821
		private ResourceSystem m_ResourceSystem;

		// Token: 0x04009F76 RID: 40822
		private SimulationSystem m_SimulationSystem;

		// Token: 0x04009F77 RID: 40823
		private TaxSystem m_TaxSystem;

		// Token: 0x04009F78 RID: 40824
		private TimeSystem m_TimeSystem;

		// Token: 0x04009F79 RID: 40825
		private CityConfigurationSystem m_CityConfigurationSystem;

		// Token: 0x04009F7A RID: 40826
		private PersonalCarSelectData m_PersonalCarSelectData;

		// Token: 0x04009F7B RID: 40827
		private CitySystem m_CitySystem;

		// Token: 0x04009F7C RID: 40828
		private CityProductionStatisticSystem m_CityProductionStatisticSystem;

		// Token: 0x04009F7D RID: 40829
		[EnumArray(typeof(ResourceBuyerSystem.BuyerOutcome))]
		[DebugWatchValue]
		private NativeArray<int> m_BuyerOutcomes;

		// Token: 0x04009F7E RID: 40830
		[EnumArray(typeof(ResourceBuyerSystem.FailedShoppingOrigin))]
		[DebugWatchValue]
		private NativeArray<int> m_FailedShoppingOrigins;

		// Token: 0x04009F7F RID: 40831
		[EnumArray(typeof(ResourceInEditor))]
		[DebugWatchValue]
		private NativeArray<int> m_FailedShoppingResources;

		// Token: 0x04009F80 RID: 40832
		[DebugWatchDeps]
		private JobHandle m_WriteDeps;

		// Token: 0x04009F81 RID: 40833
		private NativeQueue<ResourceBuyerSystem.SalesEvent> m_SalesQueue;

		// Token: 0x04009F82 RID: 40834
		private ResourceBuyerSystem.TypeHandle __TypeHandle;

		// Token: 0x020015EB RID: 5611
		[Flags]
		private enum SaleFlags : byte
		{
			// Token: 0x04009F84 RID: 40836
			None = 0,
			// Token: 0x04009F85 RID: 40837
			CommercialSeller = 1,
			// Token: 0x04009F86 RID: 40838
			ImportFromOC = 2,
			// Token: 0x04009F87 RID: 40839
			Virtual = 4
		}

		// Token: 0x020015EC RID: 5612
		private enum BuyerOutcome
		{
			// Token: 0x04009F89 RID: 40841
			CitizenSearchStarted,
			// Token: 0x04009F8A RID: 40842
			CitizenSuccess,
			// Token: 0x04009F8B RID: 40843
			CitizenInsufficientStock,
			// Token: 0x04009F8C RID: 40844
			CitizenCantAfford,
			// Token: 0x04009F8D RID: 40845
			CitizenPathfindFailed,
			// Token: 0x04009F8E RID: 40846
			CitizenStaleDestination,
			// Token: 0x04009F8F RID: 40847
			CitizenAtOutsideConnection,
			// Token: 0x04009F90 RID: 40848
			CompanySearchStarted,
			// Token: 0x04009F91 RID: 40849
			CompanySuccess,
			// Token: 0x04009F92 RID: 40850
			CompanyInsufficientStock,
			// Token: 0x04009F93 RID: 40851
			CompanyPathfindFailed,
			// Token: 0x04009F94 RID: 40852
			CompanyStaleDestination,
			// Token: 0x04009F95 RID: 40853
			Count
		}

		// Token: 0x020015ED RID: 5613
		private enum FailedShoppingOrigin
		{
			// Token: 0x04009F97 RID: 40855
			NoHome,
			// Token: 0x04009F98 RID: 40856
			NoCurrentBuilding,
			// Token: 0x04009F99 RID: 40857
			AtHome,
			// Token: 0x04009F9A RID: 40858
			AtWorkplace,
			// Token: 0x04009F9B RID: 40859
			AtOutsideConnection,
			// Token: 0x04009F9C RID: 40860
			AtOther,
			// Token: 0x04009F9D RID: 40861
			Count
		}

		// Token: 0x020015EE RID: 5614
		private struct SalesEvent
		{
			// Token: 0x04009F9E RID: 40862
			public ResourceBuyerSystem.SaleFlags m_Flags;

			// Token: 0x04009F9F RID: 40863
			public Entity m_Buyer;

			// Token: 0x04009FA0 RID: 40864
			public Entity m_Seller;

			// Token: 0x04009FA1 RID: 40865
			public Resource m_Resource;

			// Token: 0x04009FA2 RID: 40866
			public int m_Amount;

			// Token: 0x04009FA3 RID: 40867
			public float m_Distance;
		}

		// Token: 0x020015EF RID: 5615
		[BurstCompile]
		private struct BuyJob : IJob
		{
            // Token: 0x06006B83 RID: 27523 RVA: 0x003AD154 File Offset: 0x003AB354
            public SmallCity<SCShopping> m_SmallCityShopping;
            public void Execute()
			{
                m_SmallCityShopping.SetCommandBuffer(m_CommandBuffer.AsParallelWriter(), 0);
                Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(0);
				Population population = this.m_PopulationData[this.m_PopulationEntity];
				ResourceBuyerSystem.SalesEvent salesEvent;
				while (this.m_SalesQueue.TryDequeue(out salesEvent))
				{
					if (this.m_Resources.HasBuffer(salesEvent.m_Buyer) && salesEvent.m_Amount != 0)
					{
						bool flag = (salesEvent.m_Flags & ResourceBuyerSystem.SaleFlags.CommercialSeller) > ResourceBuyerSystem.SaleFlags.None;
						float num = (flag ? EconomyUtils.GetMarketPrice(salesEvent.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas) : EconomyUtils.GetIndustrialPrice(salesEvent.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas)) * (float)salesEvent.m_Amount;
						if (this.m_TradeCosts.HasBuffer(salesEvent.m_Seller))
						{
							DynamicBuffer<TradeCost> dynamicBuffer = this.m_TradeCosts[salesEvent.m_Seller];
							TradeCost tradeCost = EconomyUtils.GetTradeCost(salesEvent.m_Resource, dynamicBuffer);
							num += (float)salesEvent.m_Amount * tradeCost.m_BuyCost;
							float weight = EconomyUtils.GetWeight(salesEvent.m_Resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
							Assert.IsTrue(salesEvent.m_Amount != -1);
							float num2 = (float)EconomyUtils.GetTransportCost(salesEvent.m_Distance, salesEvent.m_Resource, salesEvent.m_Amount, weight) / (1f + (float)salesEvent.m_Amount);
							TradeCost tradeCost2 = default(TradeCost);
							if (this.m_TradeCosts.HasBuffer(salesEvent.m_Buyer))
							{
								tradeCost2 = EconomyUtils.GetTradeCost(salesEvent.m_Resource, this.m_TradeCosts[salesEvent.m_Buyer]);
							}
							if (!this.m_OutsideConnections.HasComponent(salesEvent.m_Seller) && !flag)
							{
								tradeCost.m_SellCost = math.lerp(tradeCost.m_SellCost, num2 + tradeCost2.m_SellCost, 0.5f);
								EconomyUtils.SetTradeCost(salesEvent.m_Resource, tradeCost, dynamicBuffer, true, 1f, 1f);
							}
							if (this.m_TradeCosts.HasBuffer(salesEvent.m_Buyer) && !this.m_OutsideConnections.HasComponent(salesEvent.m_Buyer))
							{
								if (num2 + tradeCost.m_BuyCost < tradeCost2.m_BuyCost)
								{
									tradeCost2.m_BuyCost = num2 + tradeCost.m_BuyCost;
								}
								else
								{
									tradeCost2.m_BuyCost = math.lerp(tradeCost2.m_BuyCost, num2 + tradeCost.m_BuyCost, 0.5f);
								}
								EconomyUtils.SetTradeCost(salesEvent.m_Resource, tradeCost2, this.m_TradeCosts[salesEvent.m_Buyer], true, 1f, 1f);
							}
						}
						if ((salesEvent.m_Flags & ResourceBuyerSystem.SaleFlags.ImportFromOC) != ResourceBuyerSystem.SaleFlags.None || !this.m_Resources.HasBuffer(salesEvent.m_Seller) || EconomyUtils.GetResources(salesEvent.m_Resource, this.m_Resources[salesEvent.m_Seller]) > 0)
						{
							if (flag && this.m_Services.HasComponent(salesEvent.m_Seller) && this.m_PropertyRenters.HasComponent(salesEvent.m_Seller))
							{
								Entity prefab = this.m_Prefabs[salesEvent.m_Seller].m_Prefab;
								ServiceAvailable serviceAvailable = this.m_Services[salesEvent.m_Seller];
								ServiceCompanyData serviceCompanyData = this.m_ServiceCompanies[prefab];
								num *= EconomyUtils.GetServicePriceMultiplier((float)serviceAvailable.m_ServiceAvailable, serviceCompanyData.m_MaxService);
								serviceAvailable.m_ServiceAvailable = math.max(0, Mathf.RoundToInt((float)(serviceAvailable.m_ServiceAvailable - salesEvent.m_Amount)));
								if (serviceAvailable.m_MeanPriority > 0f)
								{
									serviceAvailable.m_MeanPriority = math.min(1f, math.lerp(serviceAvailable.m_MeanPriority, (float)serviceAvailable.m_ServiceAvailable / (float)serviceCompanyData.m_MaxService, 0.1f));
								}
								else
								{
									serviceAvailable.m_MeanPriority = math.min(1f, (float)serviceAvailable.m_ServiceAvailable / (float)serviceCompanyData.m_MaxService);
								}
								this.m_Services[salesEvent.m_Seller] = serviceAvailable;
							}
							if (this.m_Resources.HasBuffer(salesEvent.m_Seller) && !this.m_Storages.HasComponent(salesEvent.m_Seller))
							{
								DynamicBuffer<Game.Economy.Resources> dynamicBuffer2 = this.m_Resources[salesEvent.m_Seller];
								int resources = EconomyUtils.GetResources(salesEvent.m_Resource, dynamicBuffer2);
								EconomyUtils.AddResources(salesEvent.m_Resource, -math.min(resources, Mathf.RoundToInt((float)salesEvent.m_Amount)), dynamicBuffer2);
							}
							EconomyUtils.AddResources(Resource.Money, -Mathf.RoundToInt(num), this.m_Resources[salesEvent.m_Buyer]);
							if (this.m_Households.HasComponent(salesEvent.m_Buyer))
							{
								Household household = this.m_Households[salesEvent.m_Buyer];
								household.m_Resources = (int)math.clamp((long)((float)household.m_Resources + num), -2147483648L, 2147483647L);
								household.m_ShoppedValuePerDay += (uint)num;
								this.m_Households[salesEvent.m_Buyer] = household;
								int resourceIndex = EconomyUtils.GetResourceIndex(salesEvent.m_Resource);
								ref NativeArray<int> ptr = ref this.m_CitizenConsumptionAccumulator;
								int num3 = resourceIndex;
								ptr[num3] += salesEvent.m_Amount;
								m_SmallCityShopping.Get().PrintResourceBought(salesEvent.m_Buyer, salesEvent.m_Seller, salesEvent.m_Resource,
									salesEvent.m_Amount, Mathf.RoundToInt(num));
                            }
							else if (this.m_BuyingCompanies.HasComponent(salesEvent.m_Buyer))
							{
								BuyingCompany buyingCompany = this.m_BuyingCompanies[salesEvent.m_Buyer];
								buyingCompany.m_LastTradePartner = salesEvent.m_Seller;
								this.m_BuyingCompanies[salesEvent.m_Buyer] = buyingCompany;
								if ((salesEvent.m_Flags & ResourceBuyerSystem.SaleFlags.Virtual) != ResourceBuyerSystem.SaleFlags.None)
								{
									EconomyUtils.AddResources(salesEvent.m_Resource, salesEvent.m_Amount, this.m_Resources[salesEvent.m_Buyer]);
								}
							}
							if (!this.m_Storages.HasComponent(salesEvent.m_Seller) && this.m_PropertyRenters.HasComponent(salesEvent.m_Seller))
							{
								DynamicBuffer<Game.Economy.Resources> dynamicBuffer3 = this.m_Resources[salesEvent.m_Seller];
								EconomyUtils.AddResources(Resource.Money, Mathf.RoundToInt(num), dynamicBuffer3);
							}
							if (this.m_CompanyStatistics.HasComponent(salesEvent.m_Seller))
							{
								CompanyStatisticData companyStatisticData = this.m_CompanyStatistics[salesEvent.m_Seller];
								companyStatisticData.m_CurrentNumberOfCustomers++;
								this.m_CompanyStatistics[salesEvent.m_Seller] = companyStatisticData;
							}
							if (this.m_CompanyStatistics.HasComponent(salesEvent.m_Buyer))
							{
								CompanyStatisticData companyStatisticData2 = this.m_CompanyStatistics[salesEvent.m_Buyer];
								companyStatisticData2.m_CurrentCostOfBuyingResources += math.abs((int)num);
								this.m_CompanyStatistics[salesEvent.m_Buyer] = companyStatisticData2;
							}
							if (salesEvent.m_Resource == Resource.Vehicles && salesEvent.m_Amount == HouseholdBehaviorSystem.kCarAmount && this.m_PropertyRenters.HasComponent(salesEvent.m_Seller))
							{
								Entity property = this.m_PropertyRenters[salesEvent.m_Seller].m_Property;
								if (this.m_TransformDatas.HasComponent(property) && this.m_HouseholdCitizens.HasBuffer(salesEvent.m_Buyer))
								{
									Entity buyer = salesEvent.m_Buyer;
									Game.Objects.Transform transform = this.m_TransformDatas[property];
									int length = this.m_HouseholdCitizens[buyer].Length;
									int num4 = (this.m_HouseholdAnimals.HasBuffer(buyer) ? this.m_HouseholdAnimals[buyer].Length : 0);
									int num5;
									int num6;
									if (this.m_OwnedVehicles.HasBuffer(buyer) && this.m_OwnedVehicles[buyer].Length >= 1)
									{
										num5 = random.NextInt(1, 1 + length);
										num6 = random.NextInt(1, 2 + num4);
									}
									else
									{
										num5 = length;
										num6 = 1 + num4;
									}
									if (random.NextInt(20) == 0)
									{
										num6 += 5;
									}
									Entity entity = this.m_PersonalCarSelectData.CreateVehicle(this.m_CommandBuffer, ref random, num5, num6, true, false, false, transform, property, Entity.Null, (PersonalCarFlags)0U, true, 0U);
									if (entity != Entity.Null)
									{
										this.m_CommandBuffer.AddComponent<Owner>(entity, new Owner(buyer));
										if (!this.m_OwnedVehicles.HasBuffer(buyer))
										{
											this.m_CommandBuffer.AddBuffer<OwnedVehicle>(buyer);
										}
									}
								}
							}
						}
					}
				}
			}

			// Token: 0x04009FA4 RID: 40868
			public NativeQueue<ResourceBuyerSystem.SalesEvent> m_SalesQueue;

			// Token: 0x04009FA5 RID: 40869
			public BufferLookup<Game.Economy.Resources> m_Resources;

			// Token: 0x04009FA6 RID: 40870
			public ComponentLookup<ServiceAvailable> m_Services;

			// Token: 0x04009FA7 RID: 40871
			[NativeDisableParallelForRestriction]
			public ComponentLookup<Household> m_Households;

			// Token: 0x04009FA8 RID: 40872
			[NativeDisableParallelForRestriction]
			public ComponentLookup<BuyingCompany> m_BuyingCompanies;

			// Token: 0x04009FA9 RID: 40873
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_TransformDatas;

			// Token: 0x04009FAA RID: 40874
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x04009FAB RID: 40875
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x04009FAC RID: 40876
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> m_ServiceCompanies;

			// Token: 0x04009FAD RID: 40877
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x04009FAE RID: 40878
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x04009FAF RID: 40879
			[ReadOnly]
			public BufferLookup<HouseholdAnimal> m_HouseholdAnimals;

			// Token: 0x04009FB0 RID: 40880
			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			// Token: 0x04009FB1 RID: 40881
			[ReadOnly]
			public ComponentLookup<Game.Companies.StorageCompany> m_Storages;

			// Token: 0x04009FB2 RID: 40882
			public ComponentLookup<CompanyStatisticData> m_CompanyStatistics;

			// Token: 0x04009FB3 RID: 40883
			public BufferLookup<TradeCost> m_TradeCosts;

			// Token: 0x04009FB4 RID: 40884
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x04009FB5 RID: 40885
			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			// Token: 0x04009FB6 RID: 40886
			[ReadOnly]
			public PersonalCarSelectData m_PersonalCarSelectData;

			// Token: 0x04009FB7 RID: 40887
			[ReadOnly]
			public ComponentLookup<Population> m_PopulationData;

			// Token: 0x04009FB8 RID: 40888
			public NativeArray<int> m_CitizenConsumptionAccumulator;

			// Token: 0x04009FB9 RID: 40889
			public Entity m_PopulationEntity;

			// Token: 0x04009FBA RID: 40890
			public RandomSeed m_RandomSeed;

			// Token: 0x04009FBB RID: 40891
			public EntityCommandBuffer m_CommandBuffer;

			// Token: 0x04009FBC RID: 40892
			[ReadOnly]
			public uint m_FrameIndex;
		}

		// Token: 0x020015F0 RID: 5616
		[BurstCompile]
		private struct HandleBuyersJob : IJobChunk
		{
            public SmallCity<SCShopping> m_SmallCityShopping;
            // Token: 0x06006B84 RID: 27524 RVA: 0x003AD929 File Offset: 0x003ABB29
            private static unsafe void IncrementSlot(NativeArray<int> array, int index)
			{
				Interlocked.Increment(ref UnsafeUtility.ArrayElementAsRef<int>(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks<int>(array), index));
			}

			// Token: 0x06006B85 RID: 27525 RVA: 0x003AD93D File Offset: 0x003ABB3D
			private void IncrementOutcome(ResourceBuyerSystem.BuyerOutcome outcome)
			{
				ResourceBuyerSystem.HandleBuyersJob.IncrementSlot(this.m_BuyerOutcomes, (int)outcome);
			}

			// Token: 0x06006B86 RID: 27526 RVA: 0x003AD94C File Offset: 0x003ABB4C
			private ResourceBuyerSystem.FailedShoppingOrigin ClassifyShoppingOrigin(Entity buyerEntity)
			{
				Entity entity = Entity.Null;
				HouseholdMember householdMember;
				PropertyRenter propertyRenter;
				if (this.m_HouseholdMembers.TryGetComponent(buyerEntity, out householdMember) && this.m_Properties.TryGetComponent(householdMember.m_Household, out propertyRenter))
				{
					entity = propertyRenter.m_Property;
				}
				if (entity == Entity.Null)
				{
					return ResourceBuyerSystem.FailedShoppingOrigin.NoHome;
				}
				CurrentBuilding currentBuilding;
				if (!this.m_CurrentBuildings.TryGetComponent(buyerEntity, out currentBuilding))
				{
					return ResourceBuyerSystem.FailedShoppingOrigin.NoCurrentBuilding;
				}
				Entity currentBuilding2 = currentBuilding.m_CurrentBuilding;
				if (currentBuilding2 == entity)
				{
					return ResourceBuyerSystem.FailedShoppingOrigin.AtHome;
				}
				Worker worker;
				if (this.m_Workers.TryGetComponent(buyerEntity, out worker))
				{
					PropertyRenter propertyRenter2;
					Entity entity2 = (this.m_Properties.TryGetComponent(worker.m_Workplace, out propertyRenter2) ? propertyRenter2.m_Property : worker.m_Workplace);
					if (currentBuilding2 == entity2)
					{
						return ResourceBuyerSystem.FailedShoppingOrigin.AtWorkplace;
					}
				}
				if (this.m_OutsideConnections.HasComponent(currentBuilding2))
				{
					return ResourceBuyerSystem.FailedShoppingOrigin.AtOutsideConnection;
				}
				return ResourceBuyerSystem.FailedShoppingOrigin.AtOther;
			}

			// Token: 0x06006B87 RID: 27527 RVA: 0x003ADA18 File Offset: 0x003ABC18
			private void RecordCitizenPathfindFailure(Entity buyerEntity, Resource resource)
			{
				ResourceBuyerSystem.HandleBuyersJob.IncrementSlot(this.m_FailedShoppingOrigins, (int)this.ClassifyShoppingOrigin(buyerEntity));
				int num = ((resource == Resource.NoResource) ? 0 : (EconomyUtils.GetResourceIndex(resource) + 1));
				ResourceBuyerSystem.HandleBuyersJob.IncrementSlot(this.m_FailedShoppingResources, num);
			}

			// Token: 0x06006B88 RID: 27528 RVA: 0x003ADA54 File Offset: 0x003ABC54
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
                m_SmallCityShopping.SetCommandBuffer(m_CommandBuffer, unfilteredChunkIndex);
                NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<ResourceBuyer> nativeArray2 = chunk.GetNativeArray<ResourceBuyer>(ref this.m_BuyerType);
				NativeArray<ResourceBought> nativeArray3 = chunk.GetNativeArray<ResourceBought>(ref this.m_BoughtType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripType);
				NativeArray<Citizen> nativeArray4 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<AttendingMeeting> nativeArray5 = chunk.GetNativeArray<AttendingMeeting>(ref this.m_AttendingMeetingType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				this.ProcessResourceBought(unfilteredChunkIndex, nativeArray3, nativeArray);
				this.ProcessResourceBuyer(chunk, unfilteredChunkIndex, nativeArray2, nativeArray, bufferAccessor, nativeArray4, random, nativeArray5);
			}

			// Token: 0x06006B89 RID: 27529 RVA: 0x003ADAE0 File Offset: 0x003ABCE0
			private void ProcessResourceBought(int unfilteredChunkIndex, NativeArray<ResourceBought> resourceBuyingWithTargets, NativeArray<Entity> entities)
			{
				for (int i = 0; i < resourceBuyingWithTargets.Length; i++)
				{
					Entity entity = entities[i];
					ResourceBought resourceBought = resourceBuyingWithTargets[i];
                    bool scShopping = m_SmallCityShopping.Get().IsValidPayer(entity, resourceBought.m_Payer, out byte saleFlags);
                    if ((this.m_PrefabRefData.HasComponent(resourceBought.m_Payer) || scShopping) && this.m_PrefabRefData.HasComponent(resourceBought.m_Seller)) {
						ResourceBuyerSystem.SalesEvent salesEvent = new ResourceBuyerSystem.SalesEvent
						{
							m_Amount = resourceBought.m_Amount,
							m_Buyer = resourceBought.m_Payer,
							m_Seller = resourceBought.m_Seller,
							m_Resource = resourceBought.m_Resource,
                            m_Flags = scShopping ? (ResourceBuyerSystem.SaleFlags)saleFlags : ResourceBuyerSystem.SaleFlags.None,
                            m_Distance = resourceBought.m_Distance
						};
                        if (scShopping)
							this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenSuccess);
                        this.m_SalesQueue.Enqueue(salesEvent);
					}
					this.m_CommandBuffer.RemoveComponent<ResourceBought>(unfilteredChunkIndex, entity);
				}
			}

			// Token: 0x06006B8A RID: 27530 RVA: 0x003ADBAC File Offset: 0x003ABDAC
			private void ProcessResourceBuyer(ArchetypeChunk chunk, int unfilteredChunkIndex, NativeArray<ResourceBuyer> resourceBuyingRequests, NativeArray<Entity> entities, BufferAccessor<TripNeeded> tripBuffers, NativeArray<Citizen> citizens, Unity.Mathematics.Random random, NativeArray<AttendingMeeting> meetings)
			{
				bool flag = citizens.Length > 0;
				for (int i = 0; i < resourceBuyingRequests.Length; i++)
				{
					ResourceBuyer resourceBuyer = resourceBuyingRequests[i];
					Entity entity = entities[i];
					DynamicBuffer<TripNeeded> dynamicBuffer = tripBuffers[i];
					bool flag2 = false;
					Entity entity2 = this.m_ResourcePrefabs[resourceBuyer.m_ResourceNeeded];
					if (this.m_ResourceDatas.HasComponent(entity2))
					{
						flag2 = EconomyUtils.GetWeight(resourceBuyer.m_ResourceNeeded, this.m_ResourcePrefabs, ref this.m_ResourceDatas) == 0f;
					}
					if (this.m_PathInformation.HasComponent(entity))
					{
						PathInformation pathInformation = this.m_PathInformation[entity];
						if ((pathInformation.m_State & PathFlags.Pending) == (PathFlags)0)
						{
							Entity destination = pathInformation.m_Destination;
							bool flag3 = this.m_OutsideConnections.HasComponent(destination);
							if (this.m_Properties.HasComponent(destination) || flag3)
							{
								int num;
								if (flag2 && flag3)
								{
									num = resourceBuyer.m_AmountNeeded;
								}
								else
								{
									DynamicBuffer<Game.Economy.Resources> dynamicBuffer2 = this.m_Resources[destination];
									num = EconomyUtils.GetResources(resourceBuyer.m_ResourceNeeded, dynamicBuffer2);
									if (this.m_StorageCompanies.HasComponent(destination))
									{
										int allBuyingResourcesTrucks = VehicleUtils.GetAllBuyingResourcesTrucks(destination, resourceBuyer.m_ResourceNeeded, ref this.m_DeliveryTrucks, ref this.m_GuestVehicles, ref this.m_LayoutElements);
										num -= allBuyingResourcesTrucks;
									}
									if (num <= 0 || (!flag3 && num < resourceBuyer.m_AmountNeeded / 2))
									{
										this.IncrementOutcome(flag ? ResourceBuyerSystem.BuyerOutcome.CitizenInsufficientStock : ResourceBuyerSystem.BuyerOutcome.CompanyInsufficientStock);
										this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
										goto IL_0721;
									}
								}
								resourceBuyer.m_AmountNeeded = math.min(resourceBuyer.m_AmountNeeded, num);
								bool flag4 = this.m_ServiceAvailables.HasComponent(destination);
								bool flag5 = this.m_StorageCompanies.HasComponent(destination);
								ResourceBuyerSystem.SaleFlags saleFlags = (flag4 ? ResourceBuyerSystem.SaleFlags.CommercialSeller : ResourceBuyerSystem.SaleFlags.None);
								if (flag2)
								{
									saleFlags |= ResourceBuyerSystem.SaleFlags.Virtual;
								}
								if (flag3)
								{
									saleFlags |= ResourceBuyerSystem.SaleFlags.ImportFromOC;
								}
								if (this.m_Households.HasComponent(resourceBuyer.m_Payer) && this.m_Resources.HasBuffer(resourceBuyer.m_Payer))
								{
									int num2 = math.max(0, EconomyUtils.GetResources(Resource.Money, this.m_Resources[resourceBuyer.m_Payer]) - HouseholdBehaviorSystem.kMinimumShoppingMoney);
									float marketPrice = EconomyUtils.GetMarketPrice(resourceBuyer.m_ResourceNeeded, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
									float num3 = 1.4f;
									int num4 = (((float)num2 > 0f) ? ((int)((float)num2 / (marketPrice * num3))) : 0);
									resourceBuyer.m_AmountNeeded = math.min(resourceBuyer.m_AmountNeeded, num4);
								}
								bool flag6 = resourceBuyer.m_AmountNeeded > 0;
                                int population = this.m_Populations[this.m_City].m_Population;
                                bool flag7 = citizens.Length > 0 && random.NextInt(100) < 100 - Mathf.RoundToInt(100f / math.max(1f, math.sqrt(this.m_EconomyParameterData.m_TrafficReduction * (float)population * 0.1f)));
                                bool physicalVirtual = flag && flag2 &&	m_SmallCityShopping.Get().BuyVirtualGoodsPhysically(entity);

                                bool physicalShopping = (!flag2 && !flag7) || physicalVirtual;
                                if (flag6 && (!flag || !m_SmallCityShopping.Get().IsNormalShopping(entity) || !physicalShopping || flag5)) {                                    
									this.IncrementOutcome(flag ? ResourceBuyerSystem.BuyerOutcome.CitizenSuccess : ResourceBuyerSystem.BuyerOutcome.CompanySuccess);
									ResourceBuyerSystem.SalesEvent salesEvent = new ResourceBuyerSystem.SalesEvent
									{
										m_Amount = resourceBuyer.m_AmountNeeded,
										m_Buyer = resourceBuyer.m_Payer,
										m_Seller = destination,
										m_Resource = resourceBuyer.m_ResourceNeeded,
										m_Flags = saleFlags,
										m_Distance = pathInformation.m_Distance
									};
									this.m_SalesQueue.Enqueue(salesEvent);
								}
								else if (!flag6 && flag)
								{
									this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenCantAfford);
								}
								this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								this.m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
								
                                if (flag) m_SmallCityShopping.Get().PrintShoppingIssue(entity, flag2, flag7, flag6);

                                
                                if (((!flag2 && !flag7) || physicalVirtual) && flag6) {
									this.m_CommandBuffer.AddBuffer<CurrentTrading>(unfilteredChunkIndex, entity).Add(new CurrentTrading
									{
										m_TradingResource = resourceBuyer.m_ResourceNeeded,
										m_TradingResourceAmount = resourceBuyer.m_AmountNeeded,
										m_OutsideConnectionType = (this.m_OutsideConnections.HasComponent(destination) ? BuildingUtils.GetOutsideConnectionType(destination, ref this.m_PrefabRefData, ref this.m_OutsideConnectionDatas) : OutsideConnectionTransferType.None),
										m_TradingStartFrameIndex = this.m_FrameIndex
									});                                    
                                    dynamicBuffer.Add(new TripNeeded
									{
										m_TargetAgent = destination,
										m_Purpose = (flag5 ? Purpose.CompanyShopping : Purpose.Shopping),
										m_Data = resourceBuyer.m_AmountNeeded,
										m_Resource = resourceBuyer.m_ResourceNeeded,
										m_Priority = 128
									});
                                    if (flag) m_SmallCityShopping.Get().SetShoppingStart(entity, destination, (byte)saleFlags); 
                                    if (!this.m_Targets.HasComponent(entities[i]))
									{
										this.m_CommandBuffer.AddComponent<Target>(unfilteredChunkIndex, entity, new Target
										{
											m_Target = destination
										});
									}
								}
							}
							else
							{
								bool flag8 = pathInformation.m_TotalCost < 0f;
								if (flag)
								{
                                    if (flag8)
									{
										this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenPathfindFailed);
										this.RecordCitizenPathfindFailure(entity, resourceBuyer.m_ResourceNeeded);
                                        m_SmallCityShopping.Get().PrintNoShoppingPath(entity);
                                    }
									else
									{
										this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenStaleDestination);
                                        m_SmallCityShopping.Get().PrintNoShoppingPath(entity, destination != Entity.Null);
                                    }
								}
								else
								{
									this.IncrementOutcome(flag8 ? ResourceBuyerSystem.BuyerOutcome.CompanyPathfindFailed : ResourceBuyerSystem.BuyerOutcome.CompanyStaleDestination);
								}
								this.m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
								this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								if (meetings.IsCreated)
								{
									AttendingMeeting attendingMeeting = meetings[i];
									Entity prefab = this.m_PrefabRefData[attendingMeeting.m_Meeting].m_Prefab;
									CoordinatedMeeting coordinatedMeeting = this.m_CoordinatedMeetings[attendingMeeting.m_Meeting];
									if (this.m_HaveCoordinatedMeetingDatas[prefab][coordinatedMeeting.m_Phase].m_TravelPurpose.m_Purpose == Purpose.Shopping)
									{
										coordinatedMeeting.m_Status = MeetingStatus.Done;
										this.m_CoordinatedMeetings[attendingMeeting.m_Meeting] = coordinatedMeeting;
									}
								}
							}
						}
					}
					else if ((!this.m_HouseholdMembers.HasComponent(entity) || (!this.m_TouristHouseholds.HasComponent(this.m_HouseholdMembers[entity].m_Household) && !this.m_CommuterHouseholds.HasComponent(this.m_HouseholdMembers[entity].m_Household))) && this.m_CurrentBuildings.HasComponent(entity) && this.m_OutsideConnections.HasComponent(this.m_CurrentBuildings[entity].m_CurrentBuilding) && !meetings.IsCreated)
					{
						ResourceBuyerSystem.SaleFlags saleFlags2 = ResourceBuyerSystem.SaleFlags.ImportFromOC;
						ResourceBuyerSystem.SalesEvent salesEvent2 = new ResourceBuyerSystem.SalesEvent
						{
							m_Amount = resourceBuyer.m_AmountNeeded,
							m_Buyer = resourceBuyer.m_Payer,
							m_Seller = this.m_CurrentBuildings[entity].m_CurrentBuilding,
							m_Resource = resourceBuyer.m_ResourceNeeded,
							m_Flags = saleFlags2,
							m_Distance = 0f
						};
						this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenAtOutsideConnection);
						this.m_SalesQueue.Enqueue(salesEvent2);
						this.m_CommandBuffer.RemoveComponent<ResourceBuyer>(unfilteredChunkIndex, entity);
					}
					else
					{
						Citizen citizen = default(Citizen);
						if (citizens.Length > 0)
						{
							citizen = citizens[i];
							Entity household = this.m_HouseholdMembers[entity].m_Household;
							Household household2 = this.m_Households[household];
							DynamicBuffer<HouseholdCitizen> dynamicBuffer3 = this.m_HouseholdCitizens[household];
                            m_SmallCityShopping.Get().SetShopFound(entity, false);
                            m_SmallCityShopping.Get().SetShopResourceFound(entity, false);
                            this.FindShopForCitizen(ref random, chunk, unfilteredChunkIndex, entity, resourceBuyer.m_ResourceNeeded, resourceBuyer.m_AmountNeeded, resourceBuyer.m_Flags, citizen, household2, dynamicBuffer3.Length, flag2);
						}
						else
						{
							this.FindShopForCompany(chunk, unfilteredChunkIndex, entity, resourceBuyer.m_ResourceNeeded, resourceBuyer.m_AmountNeeded, resourceBuyer.m_Flags, flag2);
						}
					}
					IL_0721:;
				}
			}

			// Token: 0x06006B8B RID: 27531 RVA: 0x003AE2EC File Offset: 0x003AC4EC
			private void FindShopForCitizen(ref Unity.Mathematics.Random random, ArchetypeChunk chunk, int index, Entity buyer, Resource resource, int amount, SetupTargetFlags flags, Citizen citizenData, Household householdData, int householdCitizenCount, bool virtualGood)
			{
				this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CitizenSearchStarted);
				if (virtualGood && !m_SmallCityShopping.Get().BuyVirtualGoodsPhysically(buyer))
				{
					flags |= SetupTargetFlags.Import;
				}
				this.m_CommandBuffer.AddComponent(index, buyer, in this.m_PathfindTypes);
				this.m_CommandBuffer.SetComponent<PathInformation>(index, buyer, new PathInformation
				{
					m_State = PathFlags.Pending
				});
				CreatureData creatureData;
				PseudoRandomSeed pseudoRandomSeed;
				Entity entity = ObjectEmergeSystem.SelectResidentPrefab(citizenData, this.m_HumanChunks, this.m_EntityType, ref this.m_CreatureDataType, ref this.m_ResidentDataType, out creatureData, out pseudoRandomSeed);
				HumanData humanData = default(HumanData);
				if (entity != Entity.Null)
				{
					humanData = this.m_PrefabHumanData[entity];
				}
				PathfindParameters pathfindParameters = new PathfindParameters
				{
					m_MaxSpeed = 277.77777f,
					m_WalkSpeed = humanData.m_WalkSpeed,
					m_Weights = CitizenUtils.GetPathfindWeights(citizenData, householdData, householdCitizenCount),
					m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay, 0.020833334f)),
					m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
					m_MaxCost = this.m_TripPriorityParameters.GetMaxCost(this.m_TripPriorityParameters.m_PriorityShopping)
				};
                /*if (virtualGood && m_SmallCityShopping.Get().BuyVirtualGoodsPhysically(buyer)) {
                    pathfindParameters.m_MaxCost = 0f;
                }*/
                SetupQueueTarget setupQueueTarget = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_RandomCost = 30f
				};
				SetupQueueTarget setupQueueTarget2 = new SetupQueueTarget
				{
					m_Type = SetupTargetType.ResourceSeller,
					m_Methods = PathMethod.Pedestrian,
					m_Resource = resource,
					m_Value = amount,
					m_Flags = flags,
					m_RandomCost = 30f,
					m_ActivityMask = creatureData.m_SupportedActivities
				};
				if (virtualGood && !m_SmallCityShopping.Get().BuyVirtualGoodsPhysically(buyer))
				{
					pathfindParameters.m_PathfindFlags |= PathfindFlags.SkipPathfind;
				}
				Entity entity2 = Entity.Null;
				HouseholdMember householdMember;
				PropertyRenter propertyRenter;
				if (this.m_HouseholdMembers.TryGetComponent(buyer, out householdMember) && this.m_Properties.TryGetComponent(householdMember.m_Household, out propertyRenter))
				{
					entity2 = propertyRenter.m_Property;
					pathfindParameters.m_Authorization1 = propertyRenter.m_Property;
				}
				if (this.m_Workers.HasComponent(buyer))
				{
					Worker worker = this.m_Workers[buyer];
					if (this.m_Properties.HasComponent(worker.m_Workplace))
					{
						pathfindParameters.m_Authorization2 = this.m_Properties[worker.m_Workplace].m_Property;
					}
					else
					{
						pathfindParameters.m_Authorization2 = worker.m_Workplace;
					}
				}
				float num = 20f;
				CurrentDistrict currentDistrict;
				DynamicBuffer<DistrictModifier> dynamicBuffer;
				if (this.m_CurrentDistrictData.TryGetComponent(entity2, out currentDistrict) && this.m_DistrictModifiers.TryGetBuffer(currentDistrict.m_District, out dynamicBuffer))
				{
					AreaUtils.ApplyModifier(ref num, dynamicBuffer, DistrictModifierType.BikeProbability);
				}
				bool flag = random.NextFloat(100f) < num;
				if (this.m_CarKeepers.IsComponentEnabled(buyer))
				{
					Entity car = this.m_CarKeepers[buyer].m_Car;
					if (this.m_ParkedCarData.HasComponent(car))
					{
						PrefabRef prefabRef = this.m_PrefabRefData[car];
						ParkedCar parkedCar = this.m_ParkedCarData[car];
						CarData carData = this.m_PrefabCarData[prefabRef.m_Prefab];
						pathfindParameters.m_MaxSpeed.x = carData.m_MaxSpeed;
						pathfindParameters.m_ParkingTarget = parkedCar.m_Lane;
						pathfindParameters.m_ParkingDelta = parkedCar.m_CurvePosition;
						pathfindParameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref this.m_PrefabRefData, ref this.m_ObjectGeometryData);
						pathfindParameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						pathfindParameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						Game.Vehicles.PersonalCar personalCar;
						if (this.m_PersonalCarData.TryGetComponent(car, out personalCar) && (personalCar.m_State & PersonalCarFlags.HomeTarget) == (PersonalCarFlags)0U)
						{
							pathfindParameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
						}
					}
				}
				else if (this.m_BicycleOwners.IsComponentEnabled(buyer) && flag)
				{
					Entity bicycle = this.m_BicycleOwners[buyer].m_Bicycle;
					PrefabRef prefabRef2;
					CurrentBuilding currentBuilding;
					if (!this.m_PrefabRefData.TryGetComponent(bicycle, out prefabRef2) && this.m_CurrentBuildings.TryGetComponent(buyer, out currentBuilding) && currentBuilding.m_CurrentBuilding == entity2)
					{
						Unity.Mathematics.Random pseudoRandom = citizenData.GetPseudoRandom((CitizenPseudoRandom)2761047266U);
						Entity entity3;
						prefabRef2.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref pseudoRandom, 1, 0, true, false, true, out entity3);
					}
					CarData carData2;
					ObjectGeometryData objectGeometryData;
					if (this.m_PrefabCarData.TryGetComponent(prefabRef2.m_Prefab, out carData2) && this.m_ObjectGeometryData.TryGetComponent(prefabRef2.m_Prefab, out objectGeometryData))
					{
						pathfindParameters.m_MaxSpeed.x = carData2.m_MaxSpeed;
						float num2;
						pathfindParameters.m_ParkingSize = VehicleUtils.GetParkingSize(objectGeometryData, out num2);
						pathfindParameters.m_Methods |= PathMethod.Bicycle | PathMethod.BicycleParking;
						pathfindParameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRulesBicycleDefaults();
						ParkedCar parkedCar2;
						if (this.m_ParkedCarData.TryGetComponent(bicycle, out parkedCar2))
						{
							pathfindParameters.m_ParkingTarget = parkedCar2.m_Lane;
							pathfindParameters.m_ParkingDelta = parkedCar2.m_CurvePosition;
							Game.Vehicles.PersonalCar personalCar2;
							if (this.m_PersonalCarData.TryGetComponent(bicycle, out personalCar2) && (personalCar2.m_State & PersonalCarFlags.HomeTarget) == (PersonalCarFlags)0U)
							{
								pathfindParameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
							}
						}
						else
						{
							setupQueueTarget.m_Methods |= PathMethod.Bicycle;
							setupQueueTarget.m_RoadTypes |= RoadTypes.Bicycle;
						}
					}
				}
				SetupQueueItem setupQueueItem = new SetupQueueItem(buyer, pathfindParameters, setupQueueTarget, setupQueueTarget2);
				this.m_PathfindQueue.Enqueue(setupQueueItem);
			}

			// Token: 0x06006B8C RID: 27532 RVA: 0x003AE814 File Offset: 0x003ACA14
			private void FindShopForCompany(ArchetypeChunk chunk, int index, Entity buyer, Resource resource, int amount, SetupTargetFlags flags, bool virtualGood)
			{
				this.IncrementOutcome(ResourceBuyerSystem.BuyerOutcome.CompanySearchStarted);
				this.m_CommandBuffer.AddComponent(index, buyer, in this.m_PathfindTypes);
				this.m_CommandBuffer.SetComponent<PathInformation>(index, buyer, new PathInformation
				{
					m_State = PathFlags.Pending
				});
				float transportCost = EconomyUtils.GetTransportCost(1f, amount, this.m_ResourceDatas[this.m_ResourcePrefabs[resource]].m_Weight, StorageTransferFlags.Car);
				PathfindParameters pathfindParameters = new PathfindParameters
				{
					m_MaxSpeed = 111.111115f,
					m_WalkSpeed = 5.555556f,
					m_Weights = new PathfindWeights(0.01f, 0.01f, transportCost, 0.01f),
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_IgnoredRules = (RuleFlags.ForbidSlowTraffic | RuleFlags.AvoidBicycles)
				};
				SetupQueueTarget setupQueueTarget = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_RoadTypes = RoadTypes.Car
				};
				SetupQueueTarget setupQueueTarget2 = new SetupQueueTarget
				{
					m_Type = SetupTargetType.ResourceSeller,
					m_Methods = (PathMethod.Road | PathMethod.CargoLoading),
					m_RoadTypes = RoadTypes.Car,
					m_Resource = resource,
					m_Value = amount,
					m_Flags = flags
				};
				if (virtualGood)
				{
					pathfindParameters.m_PathfindFlags |= PathfindFlags.SkipPathfind;
				}
				SetupQueueItem setupQueueItem = new SetupQueueItem(buyer, pathfindParameters, setupQueueTarget, setupQueueTarget2);
				this.m_PathfindQueue.Enqueue(setupQueueItem);
			}

			// Token: 0x06006B8D RID: 27533 RVA: 0x003AE978 File Offset: 0x003ACB78
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009FBD RID: 40893
			[ReadOnly]
			public ComponentTypeHandle<ResourceBuyer> m_BuyerType;

			// Token: 0x04009FBE RID: 40894
			[ReadOnly]
			public ComponentTypeHandle<ResourceBought> m_BoughtType;

			// Token: 0x04009FBF RID: 40895
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x04009FC0 RID: 40896
			public BufferTypeHandle<TripNeeded> m_TripType;

			// Token: 0x04009FC1 RID: 40897
			[ReadOnly]
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x04009FC2 RID: 40898
			[ReadOnly]
			public ComponentTypeHandle<CreatureData> m_CreatureDataType;

			// Token: 0x04009FC3 RID: 40899
			[ReadOnly]
			public ComponentTypeHandle<ResidentData> m_ResidentDataType;

			// Token: 0x04009FC4 RID: 40900
			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> m_AttendingMeetingType;

			// Token: 0x04009FC5 RID: 40901
			[ReadOnly]
			public ComponentLookup<PathInformation> m_PathInformation;

			// Token: 0x04009FC6 RID: 40902
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_Properties;

			// Token: 0x04009FC7 RID: 40903
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

			// Token: 0x04009FC8 RID: 40904
			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x04009FC9 RID: 40905
			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwners;

			// Token: 0x04009FCA RID: 40906
			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;

			// Token: 0x04009FCB RID: 40907
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;

			// Token: 0x04009FCC RID: 40908
			[ReadOnly]
			public ComponentLookup<Target> m_Targets;

			// Token: 0x04009FCD RID: 40909
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildings;

			// Token: 0x04009FCE RID: 40910
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x04009FCF RID: 40911
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x04009FD0 RID: 40912
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x04009FD1 RID: 40913
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x04009FD2 RID: 40914
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> m_CommuterHouseholds;

			// Token: 0x04009FD3 RID: 40915
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x04009FD4 RID: 40916
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.DeliveryTruck> m_DeliveryTrucks;

			// Token: 0x04009FD5 RID: 40917
			[ReadOnly]
			public ComponentLookup<Game.Companies.StorageCompany> m_StorageCompanies;

			// Token: 0x04009FD6 RID: 40918
			[ReadOnly]
			public BufferLookup<Game.Economy.Resources> m_Resources;

			// Token: 0x04009FD7 RID: 40919
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x04009FD8 RID: 40920
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x04009FD9 RID: 40921
			[ReadOnly]
			public BufferLookup<GuestVehicle> m_GuestVehicles;

			// Token: 0x04009FDA RID: 40922
			[ReadOnly]
			public BufferLookup<LayoutElement> m_LayoutElements;

			// Token: 0x04009FDB RID: 40923
			[NativeDisableParallelForRestriction]
			public ComponentLookup<CoordinatedMeeting> m_CoordinatedMeetings;

			// Token: 0x04009FDC RID: 40924
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> m_HaveCoordinatedMeetingDatas;

			// Token: 0x04009FDD RID: 40925
			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			// Token: 0x04009FDE RID: 40926
			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			// Token: 0x04009FDF RID: 40927
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			// Token: 0x04009FE0 RID: 40928
			[ReadOnly]
			public ComponentLookup<CarData> m_PrefabCarData;

			// Token: 0x04009FE1 RID: 40929
			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

			// Token: 0x04009FE2 RID: 40930
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;

			// Token: 0x04009FE3 RID: 40931
			[ReadOnly]
			public ComponentLookup<HumanData> m_PrefabHumanData;

			// Token: 0x04009FE4 RID: 40932
			[ReadOnly]
			public ComponentLookup<Population> m_Populations;

			// Token: 0x04009FE5 RID: 40933
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;

			// Token: 0x04009FE6 RID: 40934
			[ReadOnly]
			public BufferLookup<DistrictModifier> m_DistrictModifiers;

			// Token: 0x04009FE7 RID: 40935
			[ReadOnly]
			public float m_TimeOfDay;

			// Token: 0x04009FE8 RID: 40936
			[ReadOnly]
			public uint m_FrameIndex;

			// Token: 0x04009FE9 RID: 40937
			[ReadOnly]
			public RandomSeed m_RandomSeed;

			// Token: 0x04009FEA RID: 40938
			[ReadOnly]
			public ComponentTypeSet m_PathfindTypes;

			// Token: 0x04009FEB RID: 40939
			[ReadOnly]
			public NativeList<ArchetypeChunk> m_HumanChunks;

			// Token: 0x04009FEC RID: 40940
			[ReadOnly]
			public PersonalCarSelectData m_PersonalCarSelectData;

			// Token: 0x04009FED RID: 40941
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x04009FEE RID: 40942
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

			// Token: 0x04009FEF RID: 40943
			public EconomyParameterData m_EconomyParameterData;

			// Token: 0x04009FF0 RID: 40944
			[ReadOnly]
			public TripPriorityParametersData m_TripPriorityParameters;

			// Token: 0x04009FF1 RID: 40945
			public Entity m_City;

			// Token: 0x04009FF2 RID: 40946
			public NativeQueue<ResourceBuyerSystem.SalesEvent>.ParallelWriter m_SalesQueue;

			// Token: 0x04009FF3 RID: 40947
			[ReadOnly]
			public NativeArray<int> m_BuyerOutcomes;

			// Token: 0x04009FF4 RID: 40948
			[ReadOnly]
			public NativeArray<int> m_FailedShoppingOrigins;

			// Token: 0x04009FF5 RID: 40949
			[ReadOnly]
			public NativeArray<int> m_FailedShoppingResources;
		}

		// Token: 0x020015F1 RID: 5617
		private struct TypeHandle
		{
			// Token: 0x06006B8E RID: 27534 RVA: 0x003AE988 File Offset: 0x003ACB88
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Companies_ResourceBuyer_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResourceBuyer>(true);
				this.__Game_Citizens_ResourceBought_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResourceBought>(true);
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>(false);
				this.__Game_Citizens_Citizen_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(true);
				this.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CreatureData>(true);
				this.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResidentData>(true);
				this.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle = state.GetComponentTypeHandle<AttendingMeeting>(true);
				this.__Game_Companies_ServiceAvailable_RO_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(true);
				this.__Game_Pathfind_PathInformation_RO_ComponentLookup = state.GetComponentLookup<PathInformation>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Citizens_CarKeeper_RO_ComponentLookup = state.GetComponentLookup<CarKeeper>(true);
				this.__Game_Citizens_BicycleOwner_RO_ComponentLookup = state.GetComponentLookup<BicycleOwner>(true);
				this.__Game_Vehicles_ParkedCar_RO_ComponentLookup = state.GetComponentLookup<ParkedCar>(true);
				this.__Game_Vehicles_PersonalCar_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(true);
				this.__Game_Common_Target_RO_ComponentLookup = state.GetComponentLookup<Target>(true);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentLookup = state.GetComponentLookup<CurrentBuilding>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Citizens_CommuterHousehold_RO_ComponentLookup = state.GetComponentLookup<CommuterHousehold>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Vehicles_DeliveryTruck_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.DeliveryTruck>(true);
				this.__Game_Companies_StorageCompany_RO_ComponentLookup = state.GetComponentLookup<Game.Companies.StorageCompany>(true);
				this.__Game_Economy_Resources_RO_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>(true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
				this.__Game_Vehicles_GuestVehicle_RO_BufferLookup = state.GetBufferLookup<GuestVehicle>(true);
				this.__Game_Vehicles_LayoutElement_RO_BufferLookup = state.GetBufferLookup<LayoutElement>(true);
				this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Prefabs_CarData_RO_ComponentLookup = state.GetComponentLookup<CarData>(true);
				this.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup = state.GetComponentLookup<ObjectGeometryData>(true);
				this.__Game_Prefabs_HumanData_RO_ComponentLookup = state.GetComponentLookup<HumanData>(true);
				this.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup = state.GetComponentLookup<CoordinatedMeeting>(false);
				this.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup = state.GetBufferLookup<HaveCoordinatedMeetingData>(true);
				this.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup = state.GetComponentLookup<OutsideConnectionData>(true);
				this.__Game_City_Population_RW_ComponentLookup = state.GetComponentLookup<Population>(false);
				this.__Game_Areas_CurrentDistrict_RO_ComponentLookup = state.GetComponentLookup<CurrentDistrict>(true);
				this.__Game_Areas_DistrictModifier_RO_BufferLookup = state.GetBufferLookup<DistrictModifier>(true);
				this.__Game_Economy_Resources_RW_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>(false);
				this.__Game_Companies_ServiceAvailable_RW_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(false);
				this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(true);
				this.__Game_Citizens_HouseholdAnimal_RO_BufferLookup = state.GetBufferLookup<HouseholdAnimal>(true);
				this.__Game_Companies_ServiceCompanyData_RO_ComponentLookup = state.GetComponentLookup<ServiceCompanyData>(true);
				this.__Game_Citizens_Household_RW_ComponentLookup = state.GetComponentLookup<Household>(false);
				this.__Game_Companies_BuyingCompany_RW_ComponentLookup = state.GetComponentLookup<BuyingCompany>(false);
				this.__Game_Companies_TradeCost_RW_BufferLookup = state.GetBufferLookup<TradeCost>(false);
				this.__Game_Companies_CompanyStatisticData_RW_ComponentLookup = state.GetComponentLookup<CompanyStatisticData>(false);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
			}

			// Token: 0x04009FF6 RID: 40950
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009FF7 RID: 40951
			[ReadOnly]
			public ComponentTypeHandle<ResourceBuyer> __Game_Companies_ResourceBuyer_RO_ComponentTypeHandle;

			// Token: 0x04009FF8 RID: 40952
			[ReadOnly]
			public ComponentTypeHandle<ResourceBought> __Game_Citizens_ResourceBought_RO_ComponentTypeHandle;

			// Token: 0x04009FF9 RID: 40953
			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			// Token: 0x04009FFA RID: 40954
			[ReadOnly]
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RO_ComponentTypeHandle;

			// Token: 0x04009FFB RID: 40955
			[ReadOnly]
			public ComponentTypeHandle<CreatureData> __Game_Prefabs_CreatureData_RO_ComponentTypeHandle;

			// Token: 0x04009FFC RID: 40956
			[ReadOnly]
			public ComponentTypeHandle<ResidentData> __Game_Prefabs_ResidentData_RO_ComponentTypeHandle;

			// Token: 0x04009FFD RID: 40957
			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> __Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle;

			// Token: 0x04009FFE RID: 40958
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentLookup;

			// Token: 0x04009FFF RID: 40959
			[ReadOnly]
			public ComponentLookup<PathInformation> __Game_Pathfind_PathInformation_RO_ComponentLookup;

			// Token: 0x0400A000 RID: 40960
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x0400A001 RID: 40961
			[ReadOnly]
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentLookup;

			// Token: 0x0400A002 RID: 40962
			[ReadOnly]
			public ComponentLookup<BicycleOwner> __Game_Citizens_BicycleOwner_RO_ComponentLookup;

			// Token: 0x0400A003 RID: 40963
			[ReadOnly]
			public ComponentLookup<ParkedCar> __Game_Vehicles_ParkedCar_RO_ComponentLookup;

			// Token: 0x0400A004 RID: 40964
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RO_ComponentLookup;

			// Token: 0x0400A005 RID: 40965
			[ReadOnly]
			public ComponentLookup<Target> __Game_Common_Target_RO_ComponentLookup;

			// Token: 0x0400A006 RID: 40966
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			// Token: 0x0400A007 RID: 40967
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x0400A008 RID: 40968
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x0400A009 RID: 40969
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x0400A00A RID: 40970
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x0400A00B RID: 40971
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> __Game_Citizens_CommuterHousehold_RO_ComponentLookup;

			// Token: 0x0400A00C RID: 40972
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x0400A00D RID: 40973
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.DeliveryTruck> __Game_Vehicles_DeliveryTruck_RO_ComponentLookup;

			// Token: 0x0400A00E RID: 40974
			[ReadOnly]
			public ComponentLookup<Game.Companies.StorageCompany> __Game_Companies_StorageCompany_RO_ComponentLookup;

			// Token: 0x0400A00F RID: 40975
			[ReadOnly]
			public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RO_BufferLookup;

			// Token: 0x0400A010 RID: 40976
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

			// Token: 0x0400A011 RID: 40977
			[ReadOnly]
			public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;

			// Token: 0x0400A012 RID: 40978
			[ReadOnly]
			public BufferLookup<GuestVehicle> __Game_Vehicles_GuestVehicle_RO_BufferLookup;

			// Token: 0x0400A013 RID: 40979
			[ReadOnly]
			public BufferLookup<LayoutElement> __Game_Vehicles_LayoutElement_RO_BufferLookup;

			// Token: 0x0400A014 RID: 40980
			[ReadOnly]
			public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

			// Token: 0x0400A015 RID: 40981
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x0400A016 RID: 40982
			[ReadOnly]
			public ComponentLookup<CarData> __Game_Prefabs_CarData_RO_ComponentLookup;

			// Token: 0x0400A017 RID: 40983
			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> __Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;

			// Token: 0x0400A018 RID: 40984
			[ReadOnly]
			public ComponentLookup<HumanData> __Game_Prefabs_HumanData_RO_ComponentLookup;

			// Token: 0x0400A019 RID: 40985
			public ComponentLookup<CoordinatedMeeting> __Game_Citizens_CoordinatedMeeting_RW_ComponentLookup;

			// Token: 0x0400A01A RID: 40986
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> __Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup;

			// Token: 0x0400A01B RID: 40987
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> __Game_Prefabs_OutsideConnectionData_RO_ComponentLookup;

			// Token: 0x0400A01C RID: 40988
			public ComponentLookup<Population> __Game_City_Population_RW_ComponentLookup;

			// Token: 0x0400A01D RID: 40989
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> __Game_Areas_CurrentDistrict_RO_ComponentLookup;

			// Token: 0x0400A01E RID: 40990
			[ReadOnly]
			public BufferLookup<DistrictModifier> __Game_Areas_DistrictModifier_RO_BufferLookup;

			// Token: 0x0400A01F RID: 40991
			public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferLookup;

			// Token: 0x0400A020 RID: 40992
			public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RW_ComponentLookup;

			// Token: 0x0400A021 RID: 40993
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

			// Token: 0x0400A022 RID: 40994
			[ReadOnly]
			public BufferLookup<HouseholdAnimal> __Game_Citizens_HouseholdAnimal_RO_BufferLookup;

			// Token: 0x0400A023 RID: 40995
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> __Game_Companies_ServiceCompanyData_RO_ComponentLookup;

			// Token: 0x0400A024 RID: 40996
			public ComponentLookup<Household> __Game_Citizens_Household_RW_ComponentLookup;

			// Token: 0x0400A025 RID: 40997
			public ComponentLookup<BuyingCompany> __Game_Companies_BuyingCompany_RW_ComponentLookup;

			// Token: 0x0400A026 RID: 40998
			public BufferLookup<TradeCost> __Game_Companies_TradeCost_RW_BufferLookup;

			// Token: 0x0400A027 RID: 40999
			public ComponentLookup<CompanyStatisticData> __Game_Companies_CompanyStatisticData_RW_ComponentLookup;

			// Token: 0x0400A028 RID: 41000
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;
		}
	}
}
