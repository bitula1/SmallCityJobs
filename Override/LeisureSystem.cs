using System;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Game.Agents;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Events;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;

namespace BitulaMod
{
	// Token: 0x02001533 RID: 5427
	public partial class LeisureSystem : GameSystemBase
	{
		// Token: 0x06006856 RID: 26710 RVA: 0x0038C062 File Offset: 0x0038A262
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 262144 / LeisureSystem.kUpdatePerDay;
		}

		// Token: 0x06006857 RID: 26711 RVA: 0x0038C070 File Offset: 0x0038A270
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_PathFindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
			this.m_ClimateSystem = base.World.GetOrCreateSystemManaged<ClimateSystem>();
			this.m_AddMeetingSystem = base.World.GetOrCreateSystemManaged<AddMeetingSystem>();
			this.m_CityProductionStatisticSystem = base.World.GetOrCreateSystemManaged<CityProductionStatisticSystem>();
			this.m_CityConfigurationSystem = base.World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			this.m_PersonalCarSelectData = new PersonalCarSelectData(this);
			this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_LeisureParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<LeisureParametersData>() });
			this.m_LeisureQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<Citizen>(),
				ComponentType.ReadWrite<Leisure>(),
				ComponentType.ReadWrite<TripNeeded>(),
				ComponentType.ReadWrite<CurrentBuilding>(),
				ComponentType.Exclude<HealthProblem>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_ResidentPrefabQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<ObjectData>(),
				ComponentType.ReadOnly<HumanData>(),
				ComponentType.ReadOnly<ResidentData>(),
				ComponentType.ReadOnly<PrefabData>()
			});
			this.m_TimeDataQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TimeData>() });
			this.m_PopulationQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<Population>() });
			this.m_CarPrefabQuery = base.GetEntityQuery(new EntityQueryDesc[] { PersonalCarSelectData.GetEntityQueryDesc() });
			this.m_PathfindTypes = new ComponentTypeSet(ComponentType.ReadWrite<PathInformation>(), ComponentType.ReadWrite<PathElement>());
			this.m_LeisureQueue = new NativeQueue<LeisureEvent>(Allocator.Persistent);
			this.m_TripPriorityParametersQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TripPriorityParametersData>() });
			base.RequireForUpdate(this.m_LeisureQuery);
			base.RequireForUpdate(this.m_EconomyParameterQuery);
			base.RequireForUpdate(this.m_LeisureParameterQuery);
			base.RequireForUpdate(this.m_TripPriorityParametersQuery);
		}

		// Token: 0x06006858 RID: 26712 RVA: 0x0038C2D6 File Offset: 0x0038A4D6
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_LeisureQueue.Dispose();
			base.OnDestroy();
		}

		// Token: 0x06006859 RID: 26713 RVA: 0x0038C2EC File Offset: 0x0038A4EC
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			float value = this.m_ClimateSystem.precipitation.value;
			JobHandle jobHandle;
			this.m_PersonalCarSelectData.PreUpdate(this, this.m_CityConfigurationSystem, this.m_CarPrefabQuery, Allocator.TempJob, out jobHandle);
			LeisureSystem.LeisureJob leisureJob = default(LeisureSystem.LeisureJob);
			leisureJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			leisureJob.m_LeisureType = InternalCompilerInterface.GetComponentTypeHandle<Leisure>(ref this.__TypeHandle.__Game_Citizens_Leisure_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_TripType = InternalCompilerInterface.GetBufferTypeHandle<TripNeeded>(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_CreatureDataType = InternalCompilerInterface.GetComponentTypeHandle<CreatureData>(ref this.__TypeHandle.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_ResidentDataType = InternalCompilerInterface.GetComponentTypeHandle<ResidentData>(ref this.__TypeHandle.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			leisureJob.m_PathInfos = InternalCompilerInterface.GetComponentLookup<PathInformation>(ref this.__TypeHandle.__Game_Pathfind_PathInformation_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_CurrentBuildings = InternalCompilerInterface.GetComponentLookup<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_BuildingData = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_CarKeepers = InternalCompilerInterface.GetComponentLookup<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_BicycleOwners = InternalCompilerInterface.GetComponentLookup<BicycleOwner>(ref this.__TypeHandle.__Game_Citizens_BicycleOwner_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_ParkedCarData = InternalCompilerInterface.GetComponentLookup<ParkedCar>(ref this.__TypeHandle.__Game_Vehicles_ParkedCar_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PersonalCarData = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Targets = InternalCompilerInterface.GetComponentLookup<Target>(ref this.__TypeHandle.__Game_Common_Target_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PrefabRefs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_LeisureProviderDatas = InternalCompilerInterface.GetComponentLookup<LeisureProviderData>(ref this.__TypeHandle.__Game_Prefabs_LeisureProviderData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Students = InternalCompilerInterface.GetComponentLookup<Game.Citizens.Student>(ref this.__TypeHandle.__Game_Citizens_Student_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref base.CheckedStateRef);
			leisureJob.m_CitizenDatas = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Renters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PrefabCarData = InternalCompilerInterface.GetComponentLookup<CarData>(ref this.__TypeHandle.__Game_Prefabs_CarData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_ObjectGeometryData = InternalCompilerInterface.GetComponentLookup<ObjectGeometryData>(ref this.__TypeHandle.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PrefabHumanData = InternalCompilerInterface.GetComponentLookup<HumanData>(ref this.__TypeHandle.__Game_Prefabs_HumanData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_Purposes = InternalCompilerInterface.GetComponentLookup<TravelPurpose>(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_OutsideConnectionDatas = InternalCompilerInterface.GetComponentLookup<OutsideConnectionData>(ref this.__TypeHandle.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_IndustrialProcesses = InternalCompilerInterface.GetComponentLookup<IndustrialProcessData>(ref this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_ServiceAvailables = InternalCompilerInterface.GetComponentLookup<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_PopulationData = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
			leisureJob.m_RenterBufs = InternalCompilerInterface.GetBufferLookup<Renter>(ref this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup, ref base.CheckedStateRef);
			leisureJob.m_ConsumptionDatas = InternalCompilerInterface.GetComponentLookup<ConsumptionData>(ref this.__TypeHandle.__Game_Prefabs_ConsumptionData_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_CurrentDistrictData = InternalCompilerInterface.GetComponentLookup<CurrentDistrict>(ref this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup, ref base.CheckedStateRef);
			leisureJob.m_DistrictModifiers = InternalCompilerInterface.GetBufferLookup<DistrictModifier>(ref this.__TypeHandle.__Game_Areas_DistrictModifier_RO_BufferLookup, ref base.CheckedStateRef);
			leisureJob.m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
			leisureJob.m_TripPriorityParameters = this.m_TripPriorityParametersQuery.GetSingleton<TripPriorityParametersData>();
			leisureJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			leisureJob.m_TimeOfDay = this.m_TimeSystem.normalizedTime;
			leisureJob.m_UpdateFrameIndex = updateFrameWithInterval;
			leisureJob.m_Weather = value;
			leisureJob.m_Temperature = this.m_ClimateSystem.temperature;
			leisureJob.m_RandomSeed = RandomSeed.Next();
			leisureJob.m_PathfindTypes = this.m_PathfindTypes;
			JobHandle jobHandle2;
			leisureJob.m_HumanChunks = this.m_ResidentPrefabQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator, out jobHandle2);
			leisureJob.m_PersonalCarSelectData = this.m_PersonalCarSelectData;
			leisureJob.m_PathfindQueue = this.m_PathFindSetupSystem.GetQueue(this, 64, 0).AsParallelWriter();
			leisureJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			JobHandle jobHandle3;
			leisureJob.m_MeetingQueue = this.m_AddMeetingSystem.GetMeetingQueue(out jobHandle3).AsParallelWriter();
			leisureJob.m_LeisureQueue = this.m_LeisureQueue.AsParallelWriter();
			leisureJob.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			leisureJob.m_PopulationEntity = this.m_PopulationQuery.GetSingletonEntity();
            leisureJob.m_SmallCityJobs = SmallCityJobs.Create(ref base.CheckedStateRef, leisureJob.m_CommandBuffer);
            JobHandle jobHandle4 = leisureJob.ScheduleParallel(this.m_LeisureQuery, JobUtils.CombineDependencies(base.Dependency, jobHandle2, jobHandle3, jobHandle));
            SmallCityJobs.AddProducer(ref base.CheckedStateRef, jobHandle4);
            this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle4);
			this.m_PathFindSetupSystem.AddQueueWriter(jobHandle4);
			this.m_PersonalCarSelectData.PostUpdate(jobHandle4);
			LeisureSystem.SpendLeisureJob spendLeisureJob = default(LeisureSystem.SpendLeisureJob);
			spendLeisureJob.m_ServiceAvailables = InternalCompilerInterface.GetComponentLookup<ServiceAvailable>(ref this.__TypeHandle.__Game_Companies_ServiceAvailable_RW_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_CompanyStatisticDatas = InternalCompilerInterface.GetComponentLookup<CompanyStatisticData>(ref this.__TypeHandle.__Game_Companies_CompanyStatisticData_RW_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_Resources = InternalCompilerInterface.GetBufferLookup<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_CitizenDatas = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_IndustrialProcesses = InternalCompilerInterface.GetComponentLookup<IndustrialProcessData>(ref this.__TypeHandle.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_ServiceCompanyDatas = InternalCompilerInterface.GetComponentLookup<ServiceCompanyData>(ref this.__TypeHandle.__Game_Companies_ServiceCompanyData_RO_ComponentLookup, ref base.CheckedStateRef);
			spendLeisureJob.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
			JobHandle jobHandle5;
			spendLeisureJob.m_CitizensConsumptionAccumulator = this.m_CityProductionStatisticSystem.GetCityResourceUsageAccumulator(CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, out jobHandle5);
			spendLeisureJob.m_LeisureQueue = this.m_LeisureQueue;
			JobHandle jobHandle6 = spendLeisureJob.Schedule(JobHandle.CombineDependencies(jobHandle4, jobHandle5));
			this.m_ResourceSystem.AddPrefabsReader(jobHandle6);
			this.m_CityProductionStatisticSystem.AddCityUsageAccumulatorWriter(CityProductionStatisticSystem.CityResourceUsage.Consumer.Citizens, jobHandle6);
			base.Dependency = jobHandle6;
		}

		// Token: 0x0600685A RID: 26714 RVA: 0x0038CA9C File Offset: 0x0038AC9C
		public static void AddToTempList(NativeList<LeisureProviderData> tempProviderList, LeisureProviderData providerToAdd)
		{
			for (int i = 0; i < tempProviderList.Length; i++)
			{
				LeisureProviderData leisureProviderData = tempProviderList[i];
				if (leisureProviderData.m_LeisureType == providerToAdd.m_LeisureType && leisureProviderData.m_Resources == providerToAdd.m_Resources)
				{
					leisureProviderData.m_Efficiency += providerToAdd.m_Efficiency;
					tempProviderList[i] = leisureProviderData;
					return;
				}
			}
			tempProviderList.Add(in providerToAdd);
		}

		// Token: 0x0600685B RID: 26715 RVA: 0x0038CB04 File Offset: 0x0038AD04
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x0600685C RID: 26716 RVA: 0x0038CB25 File Offset: 0x0038AD25
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x0600685D RID: 26717 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public LeisureSystem()
		{
		}

		// Token: 0x04009771 RID: 38769
		public static readonly int kUpdatePerDay = 4096;

		// Token: 0x04009772 RID: 38770
		public static readonly float kUpdateInterval = 5f;

		// Token: 0x04009773 RID: 38771
		private SimulationSystem m_SimulationSystem;

		// Token: 0x04009774 RID: 38772
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x04009775 RID: 38773
		private PathfindSetupSystem m_PathFindSetupSystem;

		// Token: 0x04009776 RID: 38774
		private TimeSystem m_TimeSystem;

		// Token: 0x04009777 RID: 38775
		private ResourceSystem m_ResourceSystem;

		// Token: 0x04009778 RID: 38776
		private ClimateSystem m_ClimateSystem;

		// Token: 0x04009779 RID: 38777
		private AddMeetingSystem m_AddMeetingSystem;

		// Token: 0x0400977A RID: 38778
		private CityProductionStatisticSystem m_CityProductionStatisticSystem;

		// Token: 0x0400977B RID: 38779
		private CityConfigurationSystem m_CityConfigurationSystem;

		// Token: 0x0400977C RID: 38780
		private EntityQuery m_LeisureQuery;

		// Token: 0x0400977D RID: 38781
		private EntityQuery m_EconomyParameterQuery;

		// Token: 0x0400977E RID: 38782
		private EntityQuery m_LeisureParameterQuery;

		// Token: 0x0400977F RID: 38783
		private EntityQuery m_TripPriorityParametersQuery;

		// Token: 0x04009780 RID: 38784
		private EntityQuery m_ResidentPrefabQuery;

		// Token: 0x04009781 RID: 38785
		private EntityQuery m_TimeDataQuery;

		// Token: 0x04009782 RID: 38786
		private EntityQuery m_PopulationQuery;

		// Token: 0x04009783 RID: 38787
		private EntityQuery m_CarPrefabQuery;

		// Token: 0x04009784 RID: 38788
		private ComponentTypeSet m_PathfindTypes;

		// Token: 0x04009785 RID: 38789
		private NativeQueue<LeisureEvent> m_LeisureQueue;

		// Token: 0x04009786 RID: 38790
		private PersonalCarSelectData m_PersonalCarSelectData;

		// Token: 0x04009787 RID: 38791
		private LeisureSystem.TypeHandle __TypeHandle;

		// Token: 0x02001534 RID: 5428
		[BurstCompile]
		private struct SpendLeisureJob : IJob
		{
			// Token: 0x0600685F RID: 26719 RVA: 0x0038CB60 File Offset: 0x0038AD60
			public void Execute()
			{
				LeisureEvent leisureEvent;
				while (this.m_LeisureQueue.TryDequeue(out leisureEvent))
				{
					if (this.m_CitizenDatas.HasComponent(leisureEvent.m_Citizen))
					{
						Citizen citizen = this.m_CitizenDatas[leisureEvent.m_Citizen];
						int num = (int)math.ceil((float)leisureEvent.m_Efficiency / LeisureSystem.kUpdateInterval);
						citizen.m_LeisureCounter = (byte)math.min(255, (int)citizen.m_LeisureCounter + num);
						this.m_CitizenDatas[leisureEvent.m_Citizen] = citizen;
					}
					if (this.m_HouseholdMembers.HasComponent(leisureEvent.m_Citizen) && this.m_Prefabs.HasComponent(leisureEvent.m_Provider))
					{
						Entity household = this.m_HouseholdMembers[leisureEvent.m_Citizen].m_Household;
						Entity prefab = this.m_Prefabs[leisureEvent.m_Provider].m_Prefab;
						if (this.m_IndustrialProcesses.HasComponent(prefab))
						{
							Resource resource = this.m_IndustrialProcesses[prefab].m_Output.m_Resource;
							if (resource != Resource.NoResource && this.m_Resources.HasBuffer(leisureEvent.m_Provider) && this.m_Resources.HasBuffer(household))
							{
								bool flag = false;
								float marketPrice = EconomyUtils.GetMarketPrice(resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas);
								int num2 = 0;
								float num3 = 1f;
								if (this.m_ServiceAvailables.HasComponent(leisureEvent.m_Provider) && this.m_ServiceCompanyDatas.HasComponent(prefab))
								{
									ServiceAvailable serviceAvailable = this.m_ServiceAvailables[leisureEvent.m_Provider];
									ServiceCompanyData serviceCompanyData = this.m_ServiceCompanyDatas[prefab];
									num2 = math.max((int)((float)serviceCompanyData.m_ServiceConsuming / LeisureSystem.kUpdateInterval), 1);
									if (serviceAvailable.m_ServiceAvailable > 0)
									{
										serviceAvailable.m_ServiceAvailable -= num2;
										serviceAvailable.m_MeanPriority = math.lerp(serviceAvailable.m_MeanPriority, (float)serviceAvailable.m_ServiceAvailable / (float)serviceCompanyData.m_MaxService, 0.1f);
										this.m_ServiceAvailables[leisureEvent.m_Provider] = serviceAvailable;
										num3 = EconomyUtils.GetServicePriceMultiplier((float)serviceAvailable.m_ServiceAvailable, serviceCompanyData.m_MaxService);
										if (this.m_CompanyStatisticDatas.HasComponent(leisureEvent.m_Provider))
										{
											CompanyStatisticData companyStatisticData = this.m_CompanyStatisticDatas[leisureEvent.m_Provider];
											companyStatisticData.m_CurrentNumberOfCustomers++;
											this.m_CompanyStatisticDatas[leisureEvent.m_Provider] = companyStatisticData;
										}
									}
									else
									{
										flag = true;
									}
								}
								if (!flag)
								{
									DynamicBuffer<Game.Economy.Resources> dynamicBuffer = this.m_Resources[leisureEvent.m_Provider];
									num2 = math.min(EconomyUtils.GetResources(resource, dynamicBuffer), num2);
									int num4 = (int)((float)num2 * marketPrice * num3);
									DynamicBuffer<Game.Economy.Resources> dynamicBuffer2 = this.m_Resources[household];
									EconomyUtils.AddResources(resource, -num2, dynamicBuffer);
									EconomyUtils.AddResources(Resource.Money, Mathf.RoundToInt((float)num4), dynamicBuffer);
									EconomyUtils.AddResources(Resource.Money, -Mathf.RoundToInt((float)num4), dynamicBuffer2);
									ref NativeArray<int> ptr = ref this.m_CitizensConsumptionAccumulator;
									int resourceIndex = EconomyUtils.GetResourceIndex(resource);
									ptr[resourceIndex] += num2;
								}
							}
						}
					}
				}
			}

			// Token: 0x04009788 RID: 38792
			public NativeQueue<LeisureEvent> m_LeisureQueue;

			// Token: 0x04009789 RID: 38793
			public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

			// Token: 0x0400978A RID: 38794
			public ComponentLookup<CompanyStatisticData> m_CompanyStatisticDatas;

			// Token: 0x0400978B RID: 38795
			public BufferLookup<Game.Economy.Resources> m_Resources;

			// Token: 0x0400978C RID: 38796
			public ComponentLookup<Citizen> m_CitizenDatas;

			// Token: 0x0400978D RID: 38797
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x0400978E RID: 38798
			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;

			// Token: 0x0400978F RID: 38799
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x04009790 RID: 38800
			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			// Token: 0x04009791 RID: 38801
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> m_ServiceCompanyDatas;

			// Token: 0x04009792 RID: 38802
			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			// Token: 0x04009793 RID: 38803
			public NativeArray<int> m_CitizensConsumptionAccumulator;
		}

		// Token: 0x02001535 RID: 5429
		[BurstCompile]
		private struct LeisureJob : IJobChunk
		{
            public SmallCityJobs m_SmallCityJobs;
            // Token: 0x06006860 RID: 26720 RVA: 0x0038CE74 File Offset: 0x0038B074
            private void SpendLeisure(int index, Entity entity, ref Citizen citizen, ref Leisure leisure, Entity providerEntity, LeisureProviderData provider)
			{
				bool flag = this.m_BuildingData.HasComponent(providerEntity) && BuildingUtils.CheckOption(this.m_BuildingData[providerEntity], BuildingOption.Inactive);
				if (this.m_ServiceAvailables.HasComponent(providerEntity) && this.m_ServiceAvailables[providerEntity].m_ServiceAvailable <= 0)
				{
					flag = true;
				}
				Entity prefab = this.m_PrefabRefs[providerEntity].m_Prefab;
				if (!flag && this.m_IndustrialProcesses.HasComponent(prefab))
				{
					Resource resource = this.m_IndustrialProcesses[prefab].m_Output.m_Resource;
					if (resource != Resource.NoResource && this.m_Resources.HasBuffer(providerEntity) && EconomyUtils.GetResources(resource, this.m_Resources[providerEntity]) <= 0)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					this.m_LeisureQueue.Enqueue(new LeisureEvent
					{
						m_Citizen = entity,
						m_Provider = providerEntity,
						m_Efficiency = provider.m_Efficiency
					});
				}
				if ((float)citizen.m_LeisureCounter > 255f - (float)provider.m_Efficiency / LeisureSystem.kUpdateInterval || this.m_SimulationFrame >= leisure.m_LastPossibleFrame || flag)
				{
					this.m_CommandBuffer.RemoveComponent<Leisure>(index, entity);
				}
			}

			// Token: 0x06006861 RID: 26721 RVA: 0x0038CFAC File Offset: 0x0038B1AC
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Leisure> nativeArray2 = chunk.GetNativeArray<Leisure>(ref this.m_LeisureType);
				NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray<HouseholdMember>(ref this.m_HouseholdMemberType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripType);
				int population = this.m_PopulationData[this.m_PopulationEntity].m_Population;
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					Leisure leisure = nativeArray2[i];
					DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor[i];
					Citizen citizen = this.m_CitizenDatas[entity];
					bool flag = this.m_Purposes.HasComponent(entity) && this.m_Purposes[entity].m_Purpose == Purpose.Traveling;
					Entity entity2 = leisure.m_TargetAgent;
					Entity entity3 = Entity.Null;
					LeisureProviderData leisureProviderData = default(LeisureProviderData);
					if (leisure.m_TargetAgent != Entity.Null && this.m_CurrentBuildings.HasComponent(entity))
					{
						Entity currentBuilding = this.m_CurrentBuildings[entity].m_CurrentBuilding;
						if (this.m_PropertyRenters.HasComponent(leisure.m_TargetAgent) && this.m_PropertyRenters[leisure.m_TargetAgent].m_Property == currentBuilding && this.m_PrefabRefs.HasComponent(leisure.m_TargetAgent))
						{
							Entity prefab = this.m_PrefabRefs[leisure.m_TargetAgent].m_Prefab;
							if (this.m_LeisureProviderDatas.HasComponent(prefab))
							{
								entity3 = prefab;
								leisureProviderData = this.m_LeisureProviderDatas[entity3];
							}
						}
						else if (this.m_PrefabRefs.HasComponent(currentBuilding))
						{
							Entity prefab2 = this.m_PrefabRefs[currentBuilding].m_Prefab;
							entity2 = currentBuilding;
							if (this.m_LeisureProviderDatas.HasComponent(prefab2))
							{
								entity3 = prefab2;
								leisureProviderData = this.m_LeisureProviderDatas[entity3];
							}
							else if (flag && this.m_OutsideConnectionDatas.HasComponent(prefab2))
							{
								entity3 = prefab2;
								leisureProviderData = new LeisureProviderData
								{
									m_Efficiency = 20,
									m_LeisureType = LeisureType.Travel,
									m_Resources = Resource.NoResource
								};
							}
						}
					}
					if (entity3 != Entity.Null)
					{
						this.SpendLeisure(unfilteredChunkIndex, entity, ref citizen, ref leisure, entity2, leisureProviderData);
						nativeArray2[i] = leisure;
						this.m_CitizenDatas[entity] = citizen;
					}
					else if (!flag && this.m_PathInfos.HasComponent(entity))
					{
						PathInformation pathInformation = this.m_PathInfos[entity];
						if ((pathInformation.m_State & PathFlags.Pending) == (PathFlags)0)
						{
							Entity destination = pathInformation.m_Destination;
							if ((this.m_PropertyRenters.HasComponent(destination) || this.m_PrefabRefs.HasComponent(destination)) && !this.m_Targets.HasComponent(entity))
							{
								if ((!this.m_Workers.HasComponent(entity) || m_SmallCityJobs.IsTodayOffDay(citizen, ref this.m_EconomyParameters, this.m_SimulationFrame, this.m_TimeData, population) || !WorkerSystem.IsTimeToWork(citizen, this.m_Workers[entity], ref this.m_EconomyParameters, this.m_TimeOfDay)) && (!this.m_Students.HasComponent(entity) || !StudentSystem.IsTimeToStudy(citizen, this.m_Students[entity], ref this.m_EconomyParameters, this.m_TimeOfDay, this.m_SimulationFrame, this.m_TimeData, population)))
								{
									Entity prefab3 = this.m_PrefabRefs[destination].m_Prefab;
									leisureProviderData = this.m_LeisureProviderDatas[prefab3];
									if (leisureProviderData.m_Efficiency == 0)
									{
										global::UnityEngine.Debug.LogWarning(string.Format("Warning: Leisure provider {0} has zero efficiency", destination.Index));
									}
									leisure.m_TargetAgent = destination;
									nativeArray2[i] = leisure;
                                    m_SmallCityJobs.Send(entity, CustomEventType.FoundLeisureAt, destination);
                                    dynamicBuffer.Add(new TripNeeded
									{
										m_TargetAgent = destination,
										m_Purpose = Purpose.Leisure,
										m_Priority = 128
									});
									this.m_CommandBuffer.AddComponent<Target>(unfilteredChunkIndex, entity, new Target
									{
										m_Target = destination
									});
									this.m_CommandBuffer.RemoveComponent<LeisureSeekerCooldown>(unfilteredChunkIndex, entity);
								}
								else
								{
									if (this.m_Purposes.HasComponent(entity) && (this.m_Purposes[entity].m_Purpose == Purpose.Leisure || this.m_Purposes[entity].m_Purpose == Purpose.Traveling))
									{
										this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
									}
									this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity);
									this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								}
							}
							else if (!this.m_Targets.HasComponent(entity))
							{
                                LeisureType desired = m_SmallCityJobs.GetDesiredLeisureType(entity);
                                //m_SmallCityJobs.AddParameter($"Leisure type={(int)desired}");
                                //m_SmallCityJobs.Send(entity, CustomEventType.DebugMessage);								
                                m_SmallCityJobs.PrintLeisureIssue(entity, desired);
                                if (this.m_Purposes.HasComponent(entity) && (this.m_Purposes[entity].m_Purpose == Purpose.Leisure || this.m_Purposes[entity].m_Purpose == Purpose.Traveling))
								{
									this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
								}
								this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity);
								this.m_CommandBuffer.RemoveComponent(unfilteredChunkIndex, entity, in this.m_PathfindTypes);
								this.m_CommandBuffer.AddComponent<LeisureSeekerCooldown>(unfilteredChunkIndex, entity, new LeisureSeekerCooldown
								{
									m_SimulationFrame = this.m_SimulationFrame
								});
							}
						}
					}
					else if (!this.m_Purposes.HasComponent(entity))
					{
						Entity household = nativeArray3[i].m_Household;
						this.FindLeisure(unfilteredChunkIndex, entity, household, citizen, ref random, this.m_TouristHouseholds.HasComponent(household));
						nativeArray2[i] = leisure;
					}
				}
			}

			// Token: 0x06006862 RID: 26722 RVA: 0x0038D564 File Offset: 0x0038B764
			private float GetWeight(LeisureType type, int wealth, CitizenAge age)
			{
				float num = 1f;
				float num2;
				float num3;
				float num4;
				switch (type)
				{
				case LeisureType.Meals:
					num2 = 10f;
					num3 = 0.2f;
					if (age == CitizenAge.Child)
					{
						num4 = 10f;
					}
					else if (age == CitizenAge.Teen)
					{
						num4 = 25f;
					}
					else if (age == CitizenAge.Elderly)
					{
						num4 = 35f;
					}
					else
					{
						num4 = 35f;
					}
					break;
				case LeisureType.Entertainment:
					num2 = 10f;
					num3 = 0.3f;
					if (age == CitizenAge.Child)
					{
						num4 = 0f;
					}
					else if (age == CitizenAge.Teen)
					{
						num4 = 45f;
					}
					else if (age == CitizenAge.Elderly)
					{
						num4 = 10f;
					}
					else
					{
						num4 = 45f;
					}
					break;
				case LeisureType.Commercial:
					num2 = 10f;
					num3 = 0.4f;
					if (age == CitizenAge.Child)
					{
						num4 = 20f;
					}
					else if (age == CitizenAge.Teen)
					{
						num4 = 25f;
					}
					else if (age == CitizenAge.Elderly)
					{
						num4 = 25f;
					}
					else
					{
						num4 = 30f;
					}
					break;
				case LeisureType.CityIndoors:
				case LeisureType.CityPark:
				case LeisureType.CityBeach:
					num2 = 10f;
					num3 = 0f;
					if (age == CitizenAge.Child)
					{
						num4 = 30f;
					}
					else if (age == CitizenAge.Teen)
					{
						num4 = 25f;
					}
					else if (age == CitizenAge.Elderly)
					{
						num4 = 15f;
					}
					else
					{
						num4 = 30f;
					}
					if (type == LeisureType.CityIndoors)
					{
						num = 1f;
					}
					else if (type == LeisureType.CityPark)
					{
						num = 2f * (1f - 0.95f * this.m_Weather);
					}
					else
					{
						num = 0.05f + 4f * math.saturate(0.35f - this.m_Weather) * math.saturate((this.m_Temperature - 20f) / 30f);
					}
					break;
				case LeisureType.Travel:
					num2 = 1f;
					num3 = 0.5f;
					num = 0.5f + math.saturate((30f - this.m_Temperature) / 50f);
					if (age == CitizenAge.Child)
					{
						num4 = 15f;
					}
					else if (age == CitizenAge.Teen)
					{
						num4 = 15f;
					}
					else if (age == CitizenAge.Elderly)
					{
						num4 = 30f;
					}
					else
					{
						num4 = 40f;
					}
					break;
				default:
					num2 = 0f;
					num3 = 0f;
					num4 = 0f;
					num = 0f;
					break;
				}
				return num4 * num * num2 * math.smoothstep(num3, 1f, ((float)wealth + 5000f) / 10000f);
			}

			// Token: 0x06006863 RID: 26723 RVA: 0x0038D794 File Offset: 0x0038B994
			private LeisureType SelectLeisureType(Entity household, bool tourist, Citizen citizenData, ref Unity.Mathematics.Random random)
			{
				PropertyRenter propertyRenter = (this.m_Renters.HasComponent(household) ? this.m_Renters[household] : default(PropertyRenter));
				if (tourist && random.NextFloat() < 0.3f)
				{
					return LeisureType.Attractions;
				}
				if (this.m_Households.HasComponent(household) && this.m_Resources.HasBuffer(household) && this.m_HouseholdCitizens.HasBuffer(household))
				{
					int num;
					if (tourist)
					{
						num = EconomyUtils.GetResources(Resource.Money, this.m_Resources[household]);
					}
					else
					{
						num = EconomyUtils.GetHouseholdSpendableMoney(this.m_Households[household], this.m_Resources[household], ref this.m_RenterBufs, ref this.m_ConsumptionDatas, ref this.m_PrefabRefs, propertyRenter);
					}
					float num2 = 0f;
					CitizenAge age = citizenData.GetAge();
					for (int i = 0; i < 10; i++)
					{
						num2 += this.GetWeight((LeisureType)i, num, age);
					}
					float num3 = num2 * random.NextFloat();
					for (int j = 0; j < 10; j++)
					{
						num3 -= this.GetWeight((LeisureType)j, num, age);
						if (num3 <= 0.001f)
						{
							return (LeisureType)j;
						}
					}
				}
				global::UnityEngine.Debug.LogWarning("Leisure type randomization failed");
				return LeisureType.Count;
			}

			// Token: 0x06006864 RID: 26724 RVA: 0x0038D8CC File Offset: 0x0038BACC
			private void FindLeisure(int chunkIndex, Entity citizen, Entity household, Citizen citizenData, ref Unity.Mathematics.Random random, bool tourist)
			{
				LeisureType leisureType = this.SelectLeisureType(household, tourist, citizenData, ref random);
				float num = 255f - (float)citizenData.m_LeisureCounter;
				if (leisureType == LeisureType.Travel || leisureType == LeisureType.Sightseeing || leisureType == LeisureType.Attractions)
				{
					if (this.m_Purposes.HasComponent(citizen))
					{
						this.m_CommandBuffer.RemoveComponent<TravelPurpose>(chunkIndex, citizen);
					}
					this.m_MeetingQueue.Enqueue(new AddMeetingSystem.AddMeeting
					{
						m_Household = household,
						m_Type = leisureType
					});
					return;
				}
                m_SmallCityJobs.SetDesiredLeisureType(citizen, leisureType);
                m_SmallCityJobs.CreateDesiredProviderFound(citizen);
                m_SmallCityJobs.CreateAvailableProviderFound(citizen);
                this.m_CommandBuffer.AddComponent(chunkIndex, citizen, in this.m_PathfindTypes);
				this.m_CommandBuffer.SetComponent<PathInformation>(chunkIndex, citizen, new PathInformation
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
				Household household2 = this.m_Households[household];
				DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_HouseholdCitizens[household];
				PathfindParameters pathfindParameters = new PathfindParameters
				{
					m_MaxSpeed = 277.77777f,
					m_WalkSpeed = humanData.m_WalkSpeed,
					m_Weights = CitizenUtils.GetPathfindWeights(citizenData, household2, dynamicBuffer.Length),
					m_Methods = (PathMethod.Pedestrian | PathMethod.Taxi | RouteUtils.GetPublicTransportMethods(this.m_TimeOfDay, 0.020833334f)),
					m_TaxiIgnoredRules = VehicleUtils.GetIgnoredPathfindRulesTaxiDefaults(),
					m_MaxCost = this.m_TripPriorityParameters.GetMaxCost(this.m_TripPriorityParameters.GetPriority(Purpose.Leisure, citizenData))
				};
				SetupQueueTarget setupQueueTarget = new SetupQueueTarget
				{
					m_Type = SetupTargetType.CurrentLocation,
					m_Methods = PathMethod.Pedestrian,
					m_RandomCost = 30f
				};
				SetupQueueTarget setupQueueTarget2 = new SetupQueueTarget
				{
					m_Type = SetupTargetType.Leisure,
					m_Methods = PathMethod.Pedestrian,
					m_Value = (int)leisureType,
					m_Value2 = num,
					m_RandomCost = 30f,
					m_ActivityMask = creatureData.m_SupportedActivities
				};
				PropertyRenter propertyRenter;
				if (this.m_PropertyRenters.TryGetComponent(household, out propertyRenter))
				{
					pathfindParameters.m_Authorization1 = propertyRenter.m_Property;
				}
				if (this.m_Workers.HasComponent(citizen))
				{
					Worker worker = this.m_Workers[citizen];
					if (this.m_PropertyRenters.HasComponent(worker.m_Workplace))
					{
						pathfindParameters.m_Authorization2 = this.m_PropertyRenters[worker.m_Workplace].m_Property;
					}
					else
					{
						pathfindParameters.m_Authorization2 = worker.m_Workplace;
					}
				}
				float num2 = 20f;
				CurrentDistrict currentDistrict;
				DynamicBuffer<DistrictModifier> dynamicBuffer2;
				if (this.m_CurrentDistrictData.TryGetComponent(propertyRenter.m_Property, out currentDistrict) && this.m_DistrictModifiers.TryGetBuffer(currentDistrict.m_District, out dynamicBuffer2))
				{
					AreaUtils.ApplyModifier(ref num2, dynamicBuffer2, DistrictModifierType.BikeProbability);
				}
				bool flag = random.NextFloat(100f) < num2;
				if (this.m_CarKeepers.IsComponentEnabled(citizen))
				{
					Entity car = this.m_CarKeepers[citizen].m_Car;
					if (this.m_ParkedCarData.HasComponent(car))
					{
						PrefabRef prefabRef = this.m_PrefabRefs[car];
						ParkedCar parkedCar = this.m_ParkedCarData[car];
						CarData carData = this.m_PrefabCarData[prefabRef.m_Prefab];
						pathfindParameters.m_MaxSpeed.x = carData.m_MaxSpeed;
						pathfindParameters.m_ParkingTarget = parkedCar.m_Lane;
						pathfindParameters.m_ParkingDelta = parkedCar.m_CurvePosition;
						pathfindParameters.m_ParkingSize = VehicleUtils.GetParkingSize(car, ref this.m_PrefabRefs, ref this.m_ObjectGeometryData);
						pathfindParameters.m_Methods |= VehicleUtils.GetPathMethods(carData) | PathMethod.Parking;
						pathfindParameters.m_IgnoredRules = VehicleUtils.GetIgnoredPathfindRules(carData);
						Game.Vehicles.PersonalCar personalCar;
						if (this.m_PersonalCarData.TryGetComponent(car, out personalCar) && (personalCar.m_State & PersonalCarFlags.HomeTarget) == (PersonalCarFlags)0U)
						{
							pathfindParameters.m_PathfindFlags |= PathfindFlags.ParkingReset;
						}
					}
				}
				else if (this.m_BicycleOwners.IsComponentEnabled(citizen) && flag)
				{
					Entity bicycle = this.m_BicycleOwners[citizen].m_Bicycle;
					PrefabRef prefabRef2;
					CurrentBuilding currentBuilding;
					if (!this.m_PrefabRefs.TryGetComponent(bicycle, out prefabRef2) && this.m_CurrentBuildings.TryGetComponent(citizen, out currentBuilding) && currentBuilding.m_CurrentBuilding == propertyRenter.m_Property)
					{
						Unity.Mathematics.Random pseudoRandom = citizenData.GetPseudoRandom((CitizenPseudoRandom)2761047266U);
						Entity entity2;
						prefabRef2.m_Prefab = this.m_PersonalCarSelectData.SelectVehiclePrefab(ref pseudoRandom, 1, 0, true, false, true, out entity2);
					}
					CarData carData2;
					ObjectGeometryData objectGeometryData;
					if (this.m_PrefabCarData.TryGetComponent(prefabRef2.m_Prefab, out carData2) && this.m_ObjectGeometryData.TryGetComponent(prefabRef2.m_Prefab, out objectGeometryData))
					{
						pathfindParameters.m_MaxSpeed.x = carData2.m_MaxSpeed;
						float num3;
						pathfindParameters.m_ParkingSize = VehicleUtils.GetParkingSize(objectGeometryData, out num3);
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
				SetupQueueItem setupQueueItem = new SetupQueueItem(citizen, pathfindParameters, setupQueueTarget, setupQueueTarget2);
				this.m_PathfindQueue.Enqueue(setupQueueItem);
			}

			// Token: 0x06006865 RID: 26725 RVA: 0x0038DE30 File Offset: 0x0038C030
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009794 RID: 38804
			public ComponentTypeHandle<Leisure> m_LeisureType;

			// Token: 0x04009795 RID: 38805
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x04009796 RID: 38806
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			// Token: 0x04009797 RID: 38807
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x04009798 RID: 38808
			public BufferTypeHandle<TripNeeded> m_TripType;

			// Token: 0x04009799 RID: 38809
			[ReadOnly]
			public ComponentTypeHandle<CreatureData> m_CreatureDataType;

			// Token: 0x0400979A RID: 38810
			[ReadOnly]
			public ComponentTypeHandle<ResidentData> m_ResidentDataType;

			// Token: 0x0400979B RID: 38811
			[ReadOnly]
			public ComponentLookup<PathInformation> m_PathInfos;

			// Token: 0x0400979C RID: 38812
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x0400979D RID: 38813
			[ReadOnly]
			public ComponentLookup<Target> m_Targets;

			// Token: 0x0400979E RID: 38814
			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x0400979F RID: 38815
			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwners;

			// Token: 0x040097A0 RID: 38816
			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;

			// Token: 0x040097A1 RID: 38817
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCarData;

			// Token: 0x040097A2 RID: 38818
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildings;

			// Token: 0x040097A3 RID: 38819
			[ReadOnly]
			public ComponentLookup<Building> m_BuildingData;

			// Token: 0x040097A4 RID: 38820
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefs;

			// Token: 0x040097A5 RID: 38821
			[ReadOnly]
			public ComponentLookup<LeisureProviderData> m_LeisureProviderDatas;

			// Token: 0x040097A6 RID: 38822
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x040097A7 RID: 38823
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> m_Students;

			// Token: 0x040097A8 RID: 38824
			[ReadOnly]
			public BufferLookup<Game.Economy.Resources> m_Resources;

			// Token: 0x040097A9 RID: 38825
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x040097AA RID: 38826
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_Renters;

			// Token: 0x040097AB RID: 38827
			[NativeDisableParallelForRestriction]
			public ComponentLookup<Citizen> m_CitizenDatas;

			// Token: 0x040097AC RID: 38828
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x040097AD RID: 38829
			[ReadOnly]
			public ComponentLookup<CarData> m_PrefabCarData;

			// Token: 0x040097AE RID: 38830
			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

			// Token: 0x040097AF RID: 38831
			[ReadOnly]
			public ComponentLookup<HumanData> m_PrefabHumanData;

			// Token: 0x040097B0 RID: 38832
			[ReadOnly]
			public ComponentLookup<TravelPurpose> m_Purposes;

			// Token: 0x040097B1 RID: 38833
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;

			// Token: 0x040097B2 RID: 38834
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x040097B3 RID: 38835
			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;

			// Token: 0x040097B4 RID: 38836
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> m_ServiceAvailables;

			// Token: 0x040097B5 RID: 38837
			[ReadOnly]
			public ComponentLookup<Population> m_PopulationData;

			// Token: 0x040097B6 RID: 38838
			[ReadOnly]
			public BufferLookup<Renter> m_RenterBufs;

			// Token: 0x040097B7 RID: 38839
			[ReadOnly]
			public ComponentLookup<ConsumptionData> m_ConsumptionDatas;

			// Token: 0x040097B8 RID: 38840
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> m_CurrentDistrictData;

			// Token: 0x040097B9 RID: 38841
			[ReadOnly]
			public BufferLookup<DistrictModifier> m_DistrictModifiers;

			// Token: 0x040097BA RID: 38842
			[ReadOnly]
			public RandomSeed m_RandomSeed;

			// Token: 0x040097BB RID: 38843
			[ReadOnly]
			public ComponentTypeSet m_PathfindTypes;

			// Token: 0x040097BC RID: 38844
			[ReadOnly]
			public NativeList<ArchetypeChunk> m_HumanChunks;

			// Token: 0x040097BD RID: 38845
			[ReadOnly]
			public PersonalCarSelectData m_PersonalCarSelectData;

			// Token: 0x040097BE RID: 38846
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x040097BF RID: 38847
			public TripPriorityParametersData m_TripPriorityParameters;

			// Token: 0x040097C0 RID: 38848
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040097C1 RID: 38849
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

			// Token: 0x040097C2 RID: 38850
			public NativeQueue<LeisureEvent>.ParallelWriter m_LeisureQueue;

			// Token: 0x040097C3 RID: 38851
			public NativeQueue<AddMeetingSystem.AddMeeting>.ParallelWriter m_MeetingQueue;

			// Token: 0x040097C4 RID: 38852
			public uint m_SimulationFrame;

			// Token: 0x040097C5 RID: 38853
			public uint m_UpdateFrameIndex;

			// Token: 0x040097C6 RID: 38854
			public float m_TimeOfDay;

			// Token: 0x040097C7 RID: 38855
			public float m_Weather;

			// Token: 0x040097C8 RID: 38856
			public float m_Temperature;

			// Token: 0x040097C9 RID: 38857
			public Entity m_PopulationEntity;

			// Token: 0x040097CA RID: 38858
			public TimeData m_TimeData;
		}

		// Token: 0x02001536 RID: 5430
		private struct TypeHandle
		{
			// Token: 0x06006866 RID: 26726 RVA: 0x0038DE40 File Offset: 0x0038C040
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Leisure_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Leisure>(false);
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(true);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>(false);
				this.__Game_Prefabs_CreatureData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CreatureData>(true);
				this.__Game_Prefabs_ResidentData_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResidentData>(true);
				this.__Game_Pathfind_PathInformation_RO_ComponentLookup = state.GetComponentLookup<PathInformation>(true);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentLookup = state.GetComponentLookup<CurrentBuilding>(true);
				this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Citizens_CarKeeper_RO_ComponentLookup = state.GetComponentLookup<CarKeeper>(true);
				this.__Game_Citizens_BicycleOwner_RO_ComponentLookup = state.GetComponentLookup<BicycleOwner>(true);
				this.__Game_Vehicles_ParkedCar_RO_ComponentLookup = state.GetComponentLookup<ParkedCar>(true);
				this.__Game_Vehicles_PersonalCar_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(true);
				this.__Game_Common_Target_RO_ComponentLookup = state.GetComponentLookup<Target>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Prefabs_LeisureProviderData_RO_ComponentLookup = state.GetComponentLookup<LeisureProviderData>(true);
				this.__Game_Citizens_Student_RO_ComponentLookup = state.GetComponentLookup<Game.Citizens.Student>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Economy_Resources_RO_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>(true);
				this.__Game_Citizens_Citizen_RW_ComponentLookup = state.GetComponentLookup<Citizen>(false);
				this.__Game_Prefabs_CarData_RO_ComponentLookup = state.GetComponentLookup<CarData>(true);
				this.__Game_Prefabs_ObjectGeometryData_RO_ComponentLookup = state.GetComponentLookup<ObjectGeometryData>(true);
				this.__Game_Prefabs_HumanData_RO_ComponentLookup = state.GetComponentLookup<HumanData>(true);
				this.__Game_Citizens_TravelPurpose_RO_ComponentLookup = state.GetComponentLookup<TravelPurpose>(true);
				this.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup = state.GetComponentLookup<OutsideConnectionData>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Prefabs_IndustrialProcessData_RO_ComponentLookup = state.GetComponentLookup<IndustrialProcessData>(true);
				this.__Game_Companies_ServiceAvailable_RO_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(true);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
				this.__Game_Buildings_Renter_RO_BufferLookup = state.GetBufferLookup<Renter>(true);
				this.__Game_Prefabs_ConsumptionData_RO_ComponentLookup = state.GetComponentLookup<ConsumptionData>(true);
				this.__Game_Areas_CurrentDistrict_RO_ComponentLookup = state.GetComponentLookup<CurrentDistrict>(true);
				this.__Game_Areas_DistrictModifier_RO_BufferLookup = state.GetBufferLookup<DistrictModifier>(true);
				this.__Game_Companies_ServiceAvailable_RW_ComponentLookup = state.GetComponentLookup<ServiceAvailable>(false);
				this.__Game_Companies_CompanyStatisticData_RW_ComponentLookup = state.GetComponentLookup<CompanyStatisticData>(false);
				this.__Game_Economy_Resources_RW_BufferLookup = state.GetBufferLookup<Game.Economy.Resources>(false);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
				this.__Game_Companies_ServiceCompanyData_RO_ComponentLookup = state.GetComponentLookup<ServiceCompanyData>(true);
			}

			// Token: 0x040097CB RID: 38859
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x040097CC RID: 38860
			public ComponentTypeHandle<Leisure> __Game_Citizens_Leisure_RW_ComponentTypeHandle;

			// Token: 0x040097CD RID: 38861
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			// Token: 0x040097CE RID: 38862
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x040097CF RID: 38863
			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			// Token: 0x040097D0 RID: 38864
			[ReadOnly]
			public ComponentTypeHandle<CreatureData> __Game_Prefabs_CreatureData_RO_ComponentTypeHandle;

			// Token: 0x040097D1 RID: 38865
			[ReadOnly]
			public ComponentTypeHandle<ResidentData> __Game_Prefabs_ResidentData_RO_ComponentTypeHandle;

			// Token: 0x040097D2 RID: 38866
			[ReadOnly]
			public ComponentLookup<PathInformation> __Game_Pathfind_PathInformation_RO_ComponentLookup;

			// Token: 0x040097D3 RID: 38867
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			// Token: 0x040097D4 RID: 38868
			[ReadOnly]
			public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

			// Token: 0x040097D5 RID: 38869
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x040097D6 RID: 38870
			[ReadOnly]
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentLookup;

			// Token: 0x040097D7 RID: 38871
			[ReadOnly]
			public ComponentLookup<BicycleOwner> __Game_Citizens_BicycleOwner_RO_ComponentLookup;

			// Token: 0x040097D8 RID: 38872
			[ReadOnly]
			public ComponentLookup<ParkedCar> __Game_Vehicles_ParkedCar_RO_ComponentLookup;

			// Token: 0x040097D9 RID: 38873
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RO_ComponentLookup;

			// Token: 0x040097DA RID: 38874
			[ReadOnly]
			public ComponentLookup<Target> __Game_Common_Target_RO_ComponentLookup;

			// Token: 0x040097DB RID: 38875
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x040097DC RID: 38876
			[ReadOnly]
			public ComponentLookup<LeisureProviderData> __Game_Prefabs_LeisureProviderData_RO_ComponentLookup;

			// Token: 0x040097DD RID: 38877
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> __Game_Citizens_Student_RO_ComponentLookup;

			// Token: 0x040097DE RID: 38878
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x040097DF RID: 38879
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x040097E0 RID: 38880
			[ReadOnly]
			public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RO_BufferLookup;

			// Token: 0x040097E1 RID: 38881
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RW_ComponentLookup;

			// Token: 0x040097E2 RID: 38882
			[ReadOnly]
			public ComponentLookup<CarData> __Game_Prefabs_CarData_RO_ComponentLookup;

			// Token: 0x040097E3 RID: 38883
			[ReadOnly]
			public ComponentLookup<ObjectGeometryData> __Game_Prefabs_ObjectGeometryData_RO_ComponentLookup;

			// Token: 0x040097E4 RID: 38884
			[ReadOnly]
			public ComponentLookup<HumanData> __Game_Prefabs_HumanData_RO_ComponentLookup;

			// Token: 0x040097E5 RID: 38885
			[ReadOnly]
			public ComponentLookup<TravelPurpose> __Game_Citizens_TravelPurpose_RO_ComponentLookup;

			// Token: 0x040097E6 RID: 38886
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> __Game_Prefabs_OutsideConnectionData_RO_ComponentLookup;

			// Token: 0x040097E7 RID: 38887
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x040097E8 RID: 38888
			[ReadOnly]
			public ComponentLookup<IndustrialProcessData> __Game_Prefabs_IndustrialProcessData_RO_ComponentLookup;

			// Token: 0x040097E9 RID: 38889
			[ReadOnly]
			public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RO_ComponentLookup;

			// Token: 0x040097EA RID: 38890
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;

			// Token: 0x040097EB RID: 38891
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

			// Token: 0x040097EC RID: 38892
			[ReadOnly]
			public BufferLookup<Renter> __Game_Buildings_Renter_RO_BufferLookup;

			// Token: 0x040097ED RID: 38893
			[ReadOnly]
			public ComponentLookup<ConsumptionData> __Game_Prefabs_ConsumptionData_RO_ComponentLookup;

			// Token: 0x040097EE RID: 38894
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> __Game_Areas_CurrentDistrict_RO_ComponentLookup;

			// Token: 0x040097EF RID: 38895
			[ReadOnly]
			public BufferLookup<DistrictModifier> __Game_Areas_DistrictModifier_RO_BufferLookup;

			// Token: 0x040097F0 RID: 38896
			public ComponentLookup<ServiceAvailable> __Game_Companies_ServiceAvailable_RW_ComponentLookup;

			// Token: 0x040097F1 RID: 38897
			public ComponentLookup<CompanyStatisticData> __Game_Companies_CompanyStatisticData_RW_ComponentLookup;

			// Token: 0x040097F2 RID: 38898
			public BufferLookup<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferLookup;

			// Token: 0x040097F3 RID: 38899
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x040097F4 RID: 38900
			[ReadOnly]
			public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

			// Token: 0x040097F5 RID: 38901
			[ReadOnly]
			public ComponentLookup<ServiceCompanyData> __Game_Companies_ServiceCompanyData_RO_ComponentLookup;
		}
	}
}
