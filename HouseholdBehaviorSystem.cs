using System;
using System.Runtime.CompilerServices;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Prefabs.Modes;
using Game.Tools;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;

namespace BitulaMod
{
	// Token: 0x02001519 RID: 5401
	public partial class HouseholdBehaviorSystem : GameSystemBase
	{
		// Token: 0x060067FB RID: 26619 RVA: 0x00386649 File Offset: 0x00384849
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 262144 / (HouseholdBehaviorSystem.kUpdatesPerDay * 16);
		}

		// Token: 0x060067FC RID: 26620 RVA: 0x0038665C File Offset: 0x0038485C
		protected override void OnGameLoaded(Context serializationContext)
		{
			base.OnGameLoaded(serializationContext);
			if (this.m_GameModeSettingQuery.IsEmptyIgnoreFilter)
			{
				this.m_ResourceDemandPerCitizenMultiplier = 1f;
				return;
			}
			ModeSettingData singleton = this.m_GameModeSettingQuery.GetSingleton<ModeSettingData>();
			if (singleton.m_Enable)
			{
				this.m_ResourceDemandPerCitizenMultiplier = singleton.m_ResourceDemandPerCitizenMultiplier;
				return;
			}
			this.m_ResourceDemandPerCitizenMultiplier = 1f;
		}

		// Token: 0x060067FD RID: 26621 RVA: 0x003866B8 File Offset: 0x003848B8
		public static float GetLastCommutePerCitizen(DynamicBuffer<HouseholdCitizen> householdCitizens, ComponentLookup<Worker> workers)
		{
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < householdCitizens.Length; i++)
			{
				Entity citizen = householdCitizens[i].m_Citizen;
				if (workers.HasComponent(citizen))
				{
					num2 += workers[citizen].m_LastCommuteTime;
				}
				num += 1f;
			}
			return num2 / num;
		}

		// Token: 0x060067FE RID: 26622 RVA: 0x00386716 File Offset: 0x00384916
		public static float GetConsumptionMultiplier(float2 parameter, int householdWealth)
		{
			return parameter.x + parameter.y * math.smoothstep(0f, 1f, (float)(math.max(0, householdWealth) + 1000) / 6000f);
		}

		// Token: 0x060067FF RID: 26623 RVA: 0x0038674C File Offset: 0x0038494C
		public static bool GetFreeCar(Entity household, BufferLookup<OwnedVehicle> ownedVehicles, ComponentLookup<Game.Vehicles.PersonalCar> personalCars, ref Entity car)
		{
			if (ownedVehicles.HasBuffer(household))
			{
				DynamicBuffer<OwnedVehicle> dynamicBuffer = ownedVehicles[household];
				for (int i = 0; i < dynamicBuffer.Length; i++)
				{
					car = dynamicBuffer[i].m_Vehicle;
					if (personalCars.HasComponent(car) && personalCars[car].m_Keeper.Equals(Entity.Null))
					{
						return true;
					}
				}
			}
			car = Entity.Null;
			return false;
		}

		// Token: 0x06006800 RID: 26624 RVA: 0x003867D0 File Offset: 0x003849D0
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_ResourceSystem = base.World.GetOrCreateSystemManaged<ResourceSystem>();
			this.m_TaxSystem = base.World.GetOrCreateSystemManaged<TaxSystem>();
			this.m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
			this.m_EconomyParameterGroup = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_CitizenParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<CitizenParametersData>() });
			this.m_HouseholdGroup = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<Household>(),
				ComponentType.ReadWrite<HouseholdNeed>(),
				ComponentType.ReadOnly<HouseholdCitizen>(),
				ComponentType.ReadOnly<Game.Economy.Resources>(),
				ComponentType.ReadOnly<UpdateFrame>(),
				ComponentType.Exclude<TouristHousehold>(),
				ComponentType.Exclude<MovingAway>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_GameModeSettingQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<ModeSettingData>() });
			this.m_ResourceDemandPerCitizenMultiplier = 1f;
			base.RequireForUpdate(this.m_HouseholdGroup);
			base.RequireForUpdate(this.m_EconomyParameterGroup);
			base.RequireForUpdate(this.m_CitizenParameterQuery);
		}

		// Token: 0x06006801 RID: 26625 RVA: 0x002B941D File Offset: 0x002B761D
		[Preserve]
		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

		// Token: 0x06006802 RID: 26626 RVA: 0x00386940 File Offset: 0x00384B40
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			HouseholdBehaviorSystem.HouseholdTickJob householdTickJob = default(HouseholdBehaviorSystem.HouseholdTickJob);
			householdTickJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_HouseholdType = InternalCompilerInterface.GetComponentTypeHandle<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_HouseholdNeedType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdNeed>(ref this.__TypeHandle.__Game_Citizens_HouseholdNeed_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_ResourceType = InternalCompilerInterface.GetBufferTypeHandle<Game.Economy.Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RW_BufferTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_HouseholdCitizenType = InternalCompilerInterface.GetBufferTypeHandle<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_TouristHouseholdType = InternalCompilerInterface.GetComponentTypeHandle<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_CommuterHouseholdType = InternalCompilerInterface.GetComponentTypeHandle<CommuterHousehold>(ref this.__TypeHandle.__Game_Citizens_CommuterHousehold_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_LodgingSeekerType = InternalCompilerInterface.GetComponentTypeHandle<LodgingSeeker>(ref this.__TypeHandle.__Game_Citizens_LodgingSeeker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			householdTickJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
			householdTickJob.m_RenterBufs = InternalCompilerInterface.GetBufferLookup<Renter>(ref this.__TypeHandle.__Game_Buildings_Renter_RO_BufferLookup, ref base.CheckedStateRef);
			householdTickJob.m_EconomyParameters = this.m_EconomyParameterGroup.GetSingleton<EconomyParameterData>();
			householdTickJob.m_CitizenParameters = this.m_CitizenParameterQuery.GetSingleton<CitizenParametersData>();
			householdTickJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_PropertySeekers = InternalCompilerInterface.GetComponentLookup<PropertySeeker>(ref this.__TypeHandle.__Game_Agents_PropertySeeker_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_ResourceDatas = InternalCompilerInterface.GetComponentLookup<ResourceData>(ref this.__TypeHandle.__Game_Prefabs_ResourceData_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_LodgingProviders = InternalCompilerInterface.GetComponentLookup<LodgingProvider>(ref this.__TypeHandle.__Game_Companies_LodgingProvider_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_CitizenDatas = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_Populations = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_PrefabRefs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_HealthProblems = InternalCompilerInterface.GetComponentLookup<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_ConsumptionDatas = InternalCompilerInterface.GetComponentLookup<ConsumptionData>(ref this.__TypeHandle.__Game_Prefabs_ConsumptionData_RO_ComponentLookup, ref base.CheckedStateRef);
			householdTickJob.m_ResourcePrefabs = this.m_ResourceSystem.GetPrefabs();
			householdTickJob.m_TaxRates = this.m_TaxSystem.GetTaxRates();
			householdTickJob.m_RandomSeed = RandomSeed.Next();
			householdTickJob.m_ResourceDemandPerCitizenMultiplier = this.m_ResourceDemandPerCitizenMultiplier;
			householdTickJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			householdTickJob.m_UpdateFrameIndex = updateFrameWithInterval;
			householdTickJob.m_FrameIndex = this.m_SimulationSystem.frameIndex;
			householdTickJob.m_City = this.m_CitySystem.City;
			HouseholdBehaviorSystem.HouseholdTickJob householdTickJob2 = householdTickJob;
			base.Dependency = householdTickJob2.ScheduleParallel(this.m_HouseholdGroup, base.Dependency);
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			this.m_ResourceSystem.AddPrefabsReader(base.Dependency);
			this.m_TaxSystem.AddReader(base.Dependency);
		}

		// Token: 0x06006803 RID: 26627 RVA: 0x00386CE4 File Offset: 0x00384EE4
		public static int GetAgeWeight(ResourceData resourceData, DynamicBuffer<HouseholdCitizen> citizens, ref ComponentLookup<Citizen> citizenDatas)
		{
			int num = 0;
			for (int i = 0; i < citizens.Length; i++)
			{
				Entity citizen = citizens[i].m_Citizen;
				CitizenAge age = citizenDatas[citizen].GetAge();
				if (age == CitizenAge.Child)
				{
					num += resourceData.m_ChildWeight;
				}
				else if (age == CitizenAge.Teen)
				{
					num += resourceData.m_TeenWeight;
				}
				else if (age == CitizenAge.Elderly)
				{
					num += resourceData.m_ElderlyWeight;
				}
				else
				{
					num += resourceData.m_AdultWeight;
				}
			}
			return num;
		}

		// Token: 0x06006804 RID: 26628 RVA: 0x00386D5C File Offset: 0x00384F5C
		public static int GetResourceShopWeightWithAge(int wealth, Resource resource, ResourcePrefabs resourcePrefabs, ref ComponentLookup<ResourceData> resourceDatas, int carCount, bool leisureIncluded, DynamicBuffer<HouseholdCitizen> citizens, ref ComponentLookup<Citizen> citizenDatas)
		{
			ResourceData resourceData = resourceDatas[resourcePrefabs[resource]];
			return HouseholdBehaviorSystem.GetResourceShopWeightWithAge(wealth, resourceData, carCount, leisureIncluded, citizens, ref citizenDatas);
		}

		// Token: 0x06006805 RID: 26629 RVA: 0x00386D88 File Offset: 0x00384F88
		public static int GetResourceShopWeightWithAge(int wealth, ResourceData resourceData, int carCount, bool leisureIncluded, DynamicBuffer<HouseholdCitizen> citizens, ref ComponentLookup<Citizen> citizenDatas)
		{
			float num = ((leisureIncluded || !resourceData.m_IsLeisure) ? resourceData.m_BaseConsumption : 0f);
			num += (float)(carCount * resourceData.m_CarConsumption);
			float num2 = ((leisureIncluded || !resourceData.m_IsLeisure) ? resourceData.m_WealthModifier : 0f);
			float num3 = (float)HouseholdBehaviorSystem.GetAgeWeight(resourceData, citizens, ref citizenDatas);
			return Mathf.RoundToInt(100f * num3 * num * math.smoothstep(num2, 1f, math.max(0.01f, ((float)wealth + 5000f) / 10000f)));
		}

		// Token: 0x06006806 RID: 26630 RVA: 0x00386E14 File Offset: 0x00385014
		public static int GetWeight(int wealth, Resource resource, ResourcePrefabs resourcePrefabs, ref ComponentLookup<ResourceData> resourceDatas, int carCount, bool leisureIncluded)
		{
			ResourceData resourceData = resourceDatas[resourcePrefabs[resource]];
			return HouseholdBehaviorSystem.GetWeight(wealth, resourceData, carCount, leisureIncluded);
		}

		// Token: 0x06006807 RID: 26631 RVA: 0x00386E3C File Offset: 0x0038503C
		public static int GetWeight(int wealth, ResourceData resourceData, int carCount, bool leisureIncluded)
		{
			float num = ((leisureIncluded || !resourceData.m_IsLeisure) ? resourceData.m_BaseConsumption : 0f) + (float)(carCount * resourceData.m_CarConsumption);
			float num2 = ((leisureIncluded || !resourceData.m_IsLeisure) ? resourceData.m_WealthModifier : 0f);
			return Mathf.RoundToInt(num * math.smoothstep(num2, 1f, math.clamp(((float)wealth + 5000f) / 10000f, 0.1f, 0.9f)));
		}

		// Token: 0x06006808 RID: 26632 RVA: 0x00386EB4 File Offset: 0x003850B4
		public static int GetHighestEducation(DynamicBuffer<HouseholdCitizen> citizenBuffer, ref ComponentLookup<Citizen> citizens)
		{
			int num = 0;
			for (int i = 0; i < citizenBuffer.Length; i++)
			{
				Entity citizen = citizenBuffer[i].m_Citizen;
				if (citizens.HasComponent(citizen))
				{
					Citizen citizen2 = citizens[citizen];
					CitizenAge age = citizen2.GetAge();
					if (age == CitizenAge.Teen || age == CitizenAge.Adult)
					{
						num = math.max(num, citizen2.GetEducationLevel());
					}
				}
			}
			return num;
		}

		// Token: 0x06006809 RID: 26633 RVA: 0x00386F18 File Offset: 0x00385118
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x0600680A RID: 26634 RVA: 0x00386F39 File Offset: 0x00385139
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x0600680B RID: 26635 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public HouseholdBehaviorSystem()
		{
		}

		// Token: 0x040095B9 RID: 38329
		public static readonly int kCarAmount = 50;

		// Token: 0x040095BA RID: 38330
		public static readonly int kUpdatesPerDay = 256;

		// Token: 0x040095BB RID: 38331
		public static readonly int kMaxShoppingPossibility = 80;

		// Token: 0x040095BC RID: 38332
		public static readonly int kMaxHouseholdNeedAmount = 2000;

		// Token: 0x040095BD RID: 38333
		public static readonly int kCarBuyingMinimumMoney = 10000;

		// Token: 0x040095BE RID: 38334
		public static readonly int kMinimumShoppingMoney = 1000;

		// Token: 0x040095BF RID: 38335
		private EntityQuery m_HouseholdGroup;

		// Token: 0x040095C0 RID: 38336
		private EntityQuery m_EconomyParameterGroup;

		// Token: 0x040095C1 RID: 38337
		private EntityQuery m_CitizenParameterQuery;

		// Token: 0x040095C2 RID: 38338
		private EntityQuery m_GameModeSettingQuery;

		// Token: 0x040095C3 RID: 38339
		private SimulationSystem m_SimulationSystem;

		// Token: 0x040095C4 RID: 38340
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x040095C5 RID: 38341
		private ResourceSystem m_ResourceSystem;

		// Token: 0x040095C6 RID: 38342
		private TaxSystem m_TaxSystem;

		// Token: 0x040095C7 RID: 38343
		private CitySystem m_CitySystem;

		// Token: 0x040095C8 RID: 38344
		private float m_ResourceDemandPerCitizenMultiplier;

		// Token: 0x040095C9 RID: 38345
		private HouseholdBehaviorSystem.TypeHandle __TypeHandle;

		// Token: 0x0200151A RID: 5402
		[BurstCompile]
		private struct HouseholdTickJob : IJobChunk
		{
			// Token: 0x0600680D RID: 26637 RVA: 0x00386F96 File Offset: 0x00385196
			private bool NeedsCar(int spendableMoney, int familySize, int cars, ref Unity.Mathematics.Random random)
			{
				return spendableMoney > HouseholdBehaviorSystem.kCarBuyingMinimumMoney && (double)random.NextFloat() < (double)(-(double)math.log((float)cars + 0.1f) / 10f) + 0.1;
			}

			// Token: 0x0600680E RID: 26638 RVA: 0x00386FCC File Offset: 0x003851CC
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Household> nativeArray2 = chunk.GetNativeArray<Household>(ref this.m_HouseholdType);
				NativeArray<HouseholdNeed> nativeArray3 = chunk.GetNativeArray<HouseholdNeed>(ref this.m_HouseholdNeedType);
				BufferAccessor<HouseholdCitizen> bufferAccessor = chunk.GetBufferAccessor<HouseholdCitizen>(ref this.m_HouseholdCitizenType);
				BufferAccessor<Game.Economy.Resources> bufferAccessor2 = chunk.GetBufferAccessor<Game.Economy.Resources>(ref this.m_ResourceType);
				NativeArray<TouristHousehold> nativeArray4 = chunk.GetNativeArray<TouristHousehold>(ref this.m_TouristHouseholdType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				int population = this.m_Populations[this.m_City].m_Population;
				for (int i = 0; i < chunk.Count; i++)
				{
					Entity entity = nativeArray[i];
					Household household = nativeArray2[i];
					DynamicBuffer<HouseholdCitizen> dynamicBuffer = bufferAccessor[i];
					if (this.m_FrameIndex - household.m_LastDayFrameIndex > 262144U)
					{
						household.m_ShoppedValueLastDay = household.m_ShoppedValuePerDay;
						household.m_ShoppedValuePerDay = 0U;
						household.m_MoneySpendOnBuildingLevelingLastDay = 0;
						household.m_LastDayFrameIndex = this.m_FrameIndex;
					}
					if (dynamicBuffer.Length == 0)
					{
						this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity, default(Deleted));
					}
					else
					{
						bool flag = true;
						int num = 0;
						for (int j = 0; j < dynamicBuffer.Length; j++)
						{
							num += this.m_CitizenDatas[dynamicBuffer[j].m_Citizen].Happiness;
							if (this.m_CitizenDatas[dynamicBuffer[j].m_Citizen].GetAge() >= CitizenAge.Adult)
							{
								flag = false;
							}
						}
						num /= dynamicBuffer.Length;
						bool flag2 = (float)random.NextInt(1000) < -53.35f * (float)num + Mathf.Sqrt(95.96f * (float)num * (float)num + 1013f * (float)num + 6576f) * 5.408f - 298.5f;
						bool flag3 = chunk.Has<HomelessHousehold>();
						DynamicBuffer<Game.Economy.Resources> dynamicBuffer2 = bufferAccessor2[i];
						int householdTotalWealth = EconomyUtils.GetHouseholdTotalWealth(household, dynamicBuffer2);
						int householdIncome = EconomyUtils.GetHouseholdIncome(dynamicBuffer, ref this.m_Workers, ref this.m_CitizenDatas, ref this.m_HealthProblems, ref this.m_EconomyParameters, this.m_TaxRates);
						household.m_SalaryLastDay = householdIncome;
						MoveAwayReason moveAwayReason = (flag ? MoveAwayReason.NoAdults : (flag2 ? MoveAwayReason.NotHappy : ((householdTotalWealth + householdIncome < -1000) ? MoveAwayReason.NoMoney : MoveAwayReason.None)));
						if (moveAwayReason != MoveAwayReason.None)
						{
							CitizenUtils.HouseholdMoveAway(this.m_CommandBuffer, unfilteredChunkIndex, entity, moveAwayReason);
						}
						else
						{
							if (!flag3)
							{
								if (!this.m_PropertyRenters.HasComponent(entity) || this.m_PropertyRenters[entity].m_Property == Entity.Null)
								{
									if ((household.m_Flags & HouseholdFlags.MovedIn) != HouseholdFlags.None)
									{
										this.m_CommandBuffer.AddComponent<HomelessHousehold>(unfilteredChunkIndex, entity);
									}
								}
								else
								{
									PropertyRenter propertyRenter = this.m_PropertyRenters[entity];
									this.UpdateHouseholdNeed(chunk, unfilteredChunkIndex, nativeArray3, i, ref household, householdTotalWealth, dynamicBuffer, nativeArray4, entity, propertyRenter, dynamicBuffer2, population, ref random);
								}
							}
							else
							{
								EconomyUtils.AddResources(Resource.Money, -1, dynamicBuffer2);
							}
							if (!chunk.Has<TouristHousehold>(ref this.m_TouristHouseholdType) && !chunk.Has<CommuterHousehold>(ref this.m_CommuterHouseholdType) && !this.m_PropertySeekers.IsComponentEnabled(nativeArray[i]))
							{
								Entity householdHomeBuilding = BuildingUtils.GetHouseholdHomeBuilding(entity, ref this.m_PropertyRenters, ref this.m_HomelessHouseholds);
								if (householdHomeBuilding == Entity.Null || !this.m_RenterBufs.HasBuffer(householdHomeBuilding))
								{
									this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, nativeArray[i], true);
								}
								else
								{
									float2 lookForHomeRentIncomeIdealBand = this.m_CitizenParameters.m_LookForHomeRentIncomeIdealBand;
									float2 lookForHomeChanceMultiplier = this.m_CitizenParameters.m_LookForHomeChanceMultiplier;
									int2 lookForHomeChanceClamp = this.m_CitizenParameters.m_LookForHomeChanceClamp;
									int num2 = math.clamp(Mathf.RoundToInt(this.m_CitizenParameters.m_LookForHomePopulationFactor * (float)population), lookForHomeChanceClamp.x, lookForHomeChanceClamp.y);
									PropertyRenter propertyRenter2;
									int num3 = (this.m_PropertyRenters.TryGetComponent(entity, out propertyRenter2) ? propertyRenter2.m_Rent : 0);
									float num4 = ((householdIncome > 0) ? ((float)num3 / (float)householdIncome) : 1f);
									float num5 = math.max(0.0001f, math.max(lookForHomeRentIncomeIdealBand.x, 1f - lookForHomeRentIncomeIdealBand.y));
									float num6 = math.saturate(math.max(0f, math.max(lookForHomeRentIncomeIdealBand.x - num4, num4 - lookForHomeRentIncomeIdealBand.y)) / num5);
									float num7 = math.lerp(lookForHomeChanceMultiplier.y, lookForHomeChanceMultiplier.x, num6);
									int num8 = math.max(lookForHomeChanceClamp.x, Mathf.RoundToInt((float)num2 * num7));
									if (flag3)
									{
										num8 /= math.max(1, this.m_CitizenParameters.m_LookForHomeHomelessDivisor);
									}
									if (random.NextInt(num8) == 0)
									{
										this.m_CommandBuffer.SetComponentEnabled<PropertySeeker>(unfilteredChunkIndex, nativeArray[i], true);
									}
								}
							}
							nativeArray2[i] = household;
						}
					}
				}
			}

			// Token: 0x0600680F RID: 26639 RVA: 0x003874B0 File Offset: 0x003856B0
			private void UpdateHouseholdNeed(ArchetypeChunk chunk, int unfilteredChunkIndex, NativeArray<HouseholdNeed> householdNeeds, int i, ref Household household, int totalWealth, DynamicBuffer<HouseholdCitizen> citizens, NativeArray<TouristHousehold> touristHouseholds, Entity entity, PropertyRenter propertyRenter, DynamicBuffer<Game.Economy.Resources> resources, int population, ref Unity.Mathematics.Random random)
			{
				HouseholdNeed householdNeed = householdNeeds[i];
				if (household.m_Resources > 0)
				{
					float num = HouseholdBehaviorSystem.GetConsumptionMultiplier(this.m_EconomyParameters.m_ResourceConsumptionMultiplier, totalWealth) * this.m_EconomyParameters.m_ResourceConsumptionPerCitizen * (float)citizens.Length;
					if (chunk.Has<TouristHousehold>(ref this.m_TouristHouseholdType))
					{
						num *= this.m_EconomyParameters.m_TouristConsumptionMultiplier;
						if (!chunk.Has<LodgingSeeker>(ref this.m_LodgingSeekerType))
						{
							TouristHousehold touristHousehold = touristHouseholds[i];
							if (touristHousehold.m_Hotel.Equals(Entity.Null))
							{
								this.m_CommandBuffer.AddComponent<LodgingSeeker>(unfilteredChunkIndex, entity, default(LodgingSeeker));
							}
							else if (!this.m_LodgingProviders.HasComponent(touristHousehold.m_Hotel))
							{
								touristHousehold.m_Hotel = Entity.Null;
								touristHouseholds[i] = touristHousehold;
								this.m_CommandBuffer.AddComponent<LodgingSeeker>(unfilteredChunkIndex, entity, default(LodgingSeeker));
							}
						}
					}
					int num2 = MathUtils.RoundToIntRandom(ref random, num);
					household.m_ConsumptionPerDay = (short)math.min(32767, HouseholdBehaviorSystem.kUpdatesPerDay * num2);
					household.m_Resources = math.max(household.m_Resources - num2, 0);
					return;
				}
				household.m_Resources = 0;
				household.m_ConsumptionPerDay = 0;
				if (householdNeed.m_Resource == Resource.NoResource)
				{
					int householdSpendableMoney = EconomyUtils.GetHouseholdSpendableMoney(household, resources, ref this.m_RenterBufs, ref this.m_ConsumptionDatas, ref this.m_PrefabRefs, propertyRenter);
					if (householdSpendableMoney < HouseholdBehaviorSystem.kMinimumShoppingMoney)
					{
						householdNeed.m_Amount = 0;
						householdNeed.m_Resource = Resource.NoResource;
						householdNeeds[i] = householdNeed;
						return;
					}
					int num3 = 0;
					if (this.m_OwnedVehicles.HasBuffer(entity))
					{
						num3 = this.m_OwnedVehicles[entity].Length;
					}
					ResourceIterator resourceIterator = ResourceIterator.GetIterator();
					int num4 = 0;
					while (resourceIterator.Next())
					{
						num4 += HouseholdBehaviorSystem.GetResourceShopWeightWithAge(householdSpendableMoney, resourceIterator.resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas, num3, false, citizens, ref this.m_CitizenDatas);
					}
					int num5 = random.NextInt(num4);
					resourceIterator = ResourceIterator.GetIterator();
					while (resourceIterator.Next())
					{
						int resourceShopWeightWithAge = HouseholdBehaviorSystem.GetResourceShopWeightWithAge(householdSpendableMoney, resourceIterator.resource, this.m_ResourcePrefabs, ref this.m_ResourceDatas, num3, false, citizens, ref this.m_CitizenDatas);
						num4 -= resourceShopWeightWithAge;
						if (resourceShopWeightWithAge > 0 && num4 <= num5)
						{
							if (!EconomyUtils.IsOfficeResource(resourceIterator.resource))
							{
								int num6 = math.min(HouseholdBehaviorSystem.kMaxShoppingPossibility, Mathf.RoundToInt(200f / math.max(1f, math.sqrt(this.m_EconomyParameters.m_TrafficReduction * (float)population))));
								if (household.m_ShoppedValuePerDay > 0U)
								{
									num6 /= 10;
								}
								if (random.NextInt(100) > num6)
								{
									break;
								}
							}
							if (resourceIterator.resource == Resource.Vehicles && this.NeedsCar(householdSpendableMoney, citizens.Length, num3, ref random))
							{
								householdNeed.m_Resource = Resource.Vehicles;
								householdNeed.m_Amount = HouseholdBehaviorSystem.kCarAmount;
								householdNeeds[i] = householdNeed;
								return;
							}
							householdNeed.m_Resource = resourceIterator.resource;
							float marketPrice = EconomyUtils.GetMarketPrice(this.m_ResourceDatas[this.m_ResourcePrefabs[resourceIterator.resource]]);
							householdNeed.m_Amount = math.clamp((int)((float)householdSpendableMoney / marketPrice), 0, HouseholdBehaviorSystem.kMaxHouseholdNeedAmount);
							householdNeed.m_Amount = (int)((float)householdNeed.m_Amount * this.m_ResourceDemandPerCitizenMultiplier);
							if (householdNeed.m_Amount > 0)
							{
								householdNeeds[i] = householdNeed;
								return;
							}
							break;
						}
					}
				}
			}

			// Token: 0x06006810 RID: 26640 RVA: 0x0038782C File Offset: 0x00385A2C
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040095CA RID: 38346
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040095CB RID: 38347
			public ComponentTypeHandle<Household> m_HouseholdType;

			// Token: 0x040095CC RID: 38348
			public ComponentTypeHandle<HouseholdNeed> m_HouseholdNeedType;

			// Token: 0x040095CD RID: 38349
			[ReadOnly]
			public BufferTypeHandle<HouseholdCitizen> m_HouseholdCitizenType;

			// Token: 0x040095CE RID: 38350
			public BufferTypeHandle<Game.Economy.Resources> m_ResourceType;

			// Token: 0x040095CF RID: 38351
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x040095D0 RID: 38352
			public ComponentTypeHandle<TouristHousehold> m_TouristHouseholdType;

			// Token: 0x040095D1 RID: 38353
			[ReadOnly]
			public ComponentTypeHandle<CommuterHousehold> m_CommuterHouseholdType;

			// Token: 0x040095D2 RID: 38354
			[ReadOnly]
			public ComponentTypeHandle<LodgingSeeker> m_LodgingSeekerType;

			// Token: 0x040095D3 RID: 38355
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

			// Token: 0x040095D4 RID: 38356
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x040095D5 RID: 38357
			[ReadOnly]
			public BufferLookup<Renter> m_RenterBufs;

			// Token: 0x040095D6 RID: 38358
			[ReadOnly]
			public ComponentLookup<PropertySeeker> m_PropertySeekers;

			// Token: 0x040095D7 RID: 38359
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x040095D8 RID: 38360
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x040095D9 RID: 38361
			[ReadOnly]
			public ComponentLookup<ResourceData> m_ResourceDatas;

			// Token: 0x040095DA RID: 38362
			[ReadOnly]
			public ComponentLookup<LodgingProvider> m_LodgingProviders;

			// Token: 0x040095DB RID: 38363
			[ReadOnly]
			public ComponentLookup<Population> m_Populations;

			// Token: 0x040095DC RID: 38364
			[ReadOnly]
			public ComponentLookup<Citizen> m_CitizenDatas;

			// Token: 0x040095DD RID: 38365
			[ReadOnly]
			public ComponentLookup<ConsumptionData> m_ConsumptionDatas;

			// Token: 0x040095DE RID: 38366
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefs;

			// Token: 0x040095DF RID: 38367
			[ReadOnly]
			public ComponentLookup<HealthProblem> m_HealthProblems;

			// Token: 0x040095E0 RID: 38368
			[ReadOnly]
			public ResourcePrefabs m_ResourcePrefabs;

			// Token: 0x040095E1 RID: 38369
			[ReadOnly]
			public NativeArray<int> m_TaxRates;

			// Token: 0x040095E2 RID: 38370
			public RandomSeed m_RandomSeed;

			// Token: 0x040095E3 RID: 38371
			public float m_ResourceDemandPerCitizenMultiplier;

			// Token: 0x040095E4 RID: 38372
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x040095E5 RID: 38373
			public CitizenParametersData m_CitizenParameters;

			// Token: 0x040095E6 RID: 38374
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040095E7 RID: 38375
			public uint m_UpdateFrameIndex;

			// Token: 0x040095E8 RID: 38376
			public uint m_FrameIndex;

			// Token: 0x040095E9 RID: 38377
			public Entity m_City;
		}

		// Token: 0x0200151B RID: 5403
		private struct TypeHandle
		{
			// Token: 0x06006811 RID: 26641 RVA: 0x0038783C File Offset: 0x00385A3C
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Household_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Household>(false);
				this.__Game_Citizens_HouseholdNeed_RW_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdNeed>(false);
				this.__Game_Economy_Resources_RW_BufferTypeHandle = state.GetBufferTypeHandle<Game.Economy.Resources>(false);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferTypeHandle = state.GetBufferTypeHandle<HouseholdCitizen>(true);
				this.__Game_Citizens_TouristHousehold_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TouristHousehold>(false);
				this.__Game_Citizens_CommuterHousehold_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CommuterHousehold>(true);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_LodgingSeeker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<LodgingSeeker>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
				this.__Game_Buildings_Renter_RO_BufferLookup = state.GetBufferLookup<Renter>(true);
				this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
				this.__Game_Agents_PropertySeeker_RO_ComponentLookup = state.GetComponentLookup<PropertySeeker>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Prefabs_ResourceData_RO_ComponentLookup = state.GetComponentLookup<ResourceData>(true);
				this.__Game_Companies_LodgingProvider_RO_ComponentLookup = state.GetComponentLookup<LodgingProvider>(true);
				this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Citizens_HealthProblem_RO_ComponentLookup = state.GetComponentLookup<HealthProblem>(true);
				this.__Game_Prefabs_ConsumptionData_RO_ComponentLookup = state.GetComponentLookup<ConsumptionData>(true);
			}

			// Token: 0x040095EA RID: 38378
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x040095EB RID: 38379
			public ComponentTypeHandle<Household> __Game_Citizens_Household_RW_ComponentTypeHandle;

			// Token: 0x040095EC RID: 38380
			public ComponentTypeHandle<HouseholdNeed> __Game_Citizens_HouseholdNeed_RW_ComponentTypeHandle;

			// Token: 0x040095ED RID: 38381
			public BufferTypeHandle<Game.Economy.Resources> __Game_Economy_Resources_RW_BufferTypeHandle;

			// Token: 0x040095EE RID: 38382
			[ReadOnly]
			public BufferTypeHandle<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferTypeHandle;

			// Token: 0x040095EF RID: 38383
			public ComponentTypeHandle<TouristHousehold> __Game_Citizens_TouristHousehold_RW_ComponentTypeHandle;

			// Token: 0x040095F0 RID: 38384
			[ReadOnly]
			public ComponentTypeHandle<CommuterHousehold> __Game_Citizens_CommuterHousehold_RO_ComponentTypeHandle;

			// Token: 0x040095F1 RID: 38385
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x040095F2 RID: 38386
			[ReadOnly]
			public ComponentTypeHandle<LodgingSeeker> __Game_Citizens_LodgingSeeker_RO_ComponentTypeHandle;

			// Token: 0x040095F3 RID: 38387
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x040095F4 RID: 38388
			[ReadOnly]
			public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;

			// Token: 0x040095F5 RID: 38389
			[ReadOnly]
			public BufferLookup<Renter> __Game_Buildings_Renter_RO_BufferLookup;

			// Token: 0x040095F6 RID: 38390
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

			// Token: 0x040095F7 RID: 38391
			[ReadOnly]
			public ComponentLookup<PropertySeeker> __Game_Agents_PropertySeeker_RO_ComponentLookup;

			// Token: 0x040095F8 RID: 38392
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x040095F9 RID: 38393
			[ReadOnly]
			public ComponentLookup<ResourceData> __Game_Prefabs_ResourceData_RO_ComponentLookup;

			// Token: 0x040095FA RID: 38394
			[ReadOnly]
			public ComponentLookup<LodgingProvider> __Game_Companies_LodgingProvider_RO_ComponentLookup;

			// Token: 0x040095FB RID: 38395
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x040095FC RID: 38396
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;

			// Token: 0x040095FD RID: 38397
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x040095FE RID: 38398
			[ReadOnly]
			public ComponentLookup<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentLookup;

			// Token: 0x040095FF RID: 38399
			[ReadOnly]
			public ComponentLookup<ConsumptionData> __Game_Prefabs_ConsumptionData_RO_ComponentLookup;
		}
	}
}
