using System;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Game;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Debug;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Triggers;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using UnityEngine.Scripting;

namespace BitulaMod
{
	// Token: 0x0200150D RID: 5389
	public partial class FindJobSystem : GameSystemBase
	{
		// Token: 0x060067D6 RID: 26582 RVA: 0x003561C9 File Offset: 0x003543C9
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x060067D7 RID: 26583 RVA: 0x003827C4 File Offset: 0x003809C4
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_PathfindSetupSystem = base.World.GetOrCreateSystemManaged<PathfindSetupSystem>();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
			this.m_CountHouseholdDataSystem = base.World.GetOrCreateSystemManaged<CountHouseholdDataSystem>();
			this.m_FreeQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<FreeWorkplaces>(),
				ComponentType.Exclude<Destroyed>(),
				ComponentType.Exclude<Temp>(),
				ComponentType.Exclude<Deleted>()
			});
			this.m_StartedWorking = new NativeValue<int>(Allocator.Persistent);
			this.m_JobSeekerQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<JobSeeker>(),
				ComponentType.ReadOnly<Owner>(),
				ComponentType.Exclude<PathInformation>(),
				ComponentType.Exclude<Deleted>()
			});
			this.m_ResultsQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<JobSeeker>(),
				ComponentType.ReadOnly<Owner>(),
				ComponentType.ReadOnly<PathInformation>(),
				ComponentType.Exclude<Deleted>()
			});
			this.m_FreeCache = new NativeArray<int>(5, Allocator.Persistent, NativeArrayOptions.ClearMemory);
			this.m_TripPriorityParametersQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TripPriorityParametersData>() });
			base.RequireAnyForUpdate(new EntityQuery[] { this.m_JobSeekerQuery, this.m_ResultsQuery });
			base.RequireForUpdate(this.m_TripPriorityParametersQuery);
		}

		// Token: 0x060067D8 RID: 26584 RVA: 0x0038295C File Offset: 0x00380B5C
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_StartedWorking.Dispose();
			this.m_FreeCache.Dispose();
			base.OnDestroy();
		}

		// Token: 0x060067D9 RID: 26585 RVA: 0x0038297C File Offset: 0x00380B7C
		[Preserve]
		protected override void OnUpdate()
		{
			if (!this.m_JobSeekerQuery.IsEmptyIgnoreFilter && !this.m_CountHouseholdDataSystem.IsCountDataNotReady())
			{
				FindJobSystem.CalculateFreeWorkplaceJob calculateFreeWorkplaceJob = default(FindJobSystem.CalculateFreeWorkplaceJob);
				JobHandle jobHandle;
				calculateFreeWorkplaceJob.m_FreeWorkplaces = this.m_FreeQuery.ToComponentDataListAsync<FreeWorkplaces>(base.World.UpdateAllocator.ToAllocator, out jobHandle);
				calculateFreeWorkplaceJob.m_FreeCache = this.m_FreeCache;
				jobHandle = calculateFreeWorkplaceJob.Schedule(JobHandle.CombineDependencies(jobHandle, base.Dependency));
				FindJobSystem.FindJobJob findJobJob = default(FindJobSystem.FindJobJob);
				findJobJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
				findJobJob.m_JobSeekerType = InternalCompilerInterface.GetComponentTypeHandle<JobSeeker>(ref this.__TypeHandle.__Game_Agents_JobSeeker_RW_ComponentTypeHandle, ref base.CheckedStateRef);
				findJobJob.m_OwnerType = InternalCompilerInterface.GetComponentTypeHandle<Owner>(ref this.__TypeHandle.__Game_Common_Owner_RW_ComponentTypeHandle, ref base.CheckedStateRef);
				findJobJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				findJobJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_CitizenDatas = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_Deleteds = InternalCompilerInterface.GetComponentLookup<Deleted>(ref this.__TypeHandle.__Game_Common_Deleted_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
				findJobJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
				findJobJob.m_PersonalCars = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RO_ComponentLookup, ref base.CheckedStateRef);
				findJobJob.m_PathfindQueue = this.m_PathfindSetupSystem.GetQueue(this, 80, 16).AsParallelWriter();
				findJobJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
				findJobJob.m_FreeCache = this.m_FreeCache;
				JobHandle jobHandle2;
				findJobJob.m_EmployableByEducation = this.m_CountHouseholdDataSystem.GetEmployables(out jobHandle2);
				findJobJob.m_RandomSeed = RandomSeed.Next();
				findJobJob.m_TripPriorityParameters = this.m_TripPriorityParametersQuery.GetSingleton<TripPriorityParametersData>();
                findJobJob.m_CustomEventData = CustomEventData.Create(ref base.CheckedStateRef);
                FindJobSystem.FindJobJob findJobJob2 = findJobJob;
				base.Dependency = findJobJob2.ScheduleParallel(this.m_JobSeekerQuery, JobHandle.CombineDependencies(jobHandle, base.Dependency, jobHandle2));
				this.m_PathfindSetupSystem.AddQueueWriter(base.Dependency);
				this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
				this.m_CountHouseholdDataSystem.AddHouseholdDataReader(base.Dependency);
                CustomEventData.AddProducer(ref base.CheckedStateRef, base.Dependency);
            }
			if (!this.m_ResultsQuery.IsEmptyIgnoreFilter)
			{
				FindJobSystem.StartWorkingJob startWorkingJob = default(FindJobSystem.StartWorkingJob);
				JobHandle jobHandle3;
				startWorkingJob.m_Chunks = this.m_ResultsQuery.ToArchetypeChunkListAsync(base.World.UpdateAllocator.ToAllocator, out jobHandle3);
				startWorkingJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
				startWorkingJob.m_JobSeekerType = InternalCompilerInterface.GetComponentTypeHandle<JobSeeker>(ref this.__TypeHandle.__Game_Agents_JobSeeker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				startWorkingJob.m_OwnerType = InternalCompilerInterface.GetComponentTypeHandle<Owner>(ref this.__TypeHandle.__Game_Common_Owner_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				startWorkingJob.m_PathInfoType = InternalCompilerInterface.GetComponentTypeHandle<PathInformation>(ref this.__TypeHandle.__Game_Pathfind_PathInformation_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				startWorkingJob.m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_EmployeeBuffers = InternalCompilerInterface.GetBufferLookup<Employee>(ref this.__TypeHandle.__Game_Companies_Employee_RW_BufferLookup, ref base.CheckedStateRef);
				startWorkingJob.m_FreeWorkplaces = InternalCompilerInterface.GetComponentLookup<FreeWorkplaces>(ref this.__TypeHandle.__Game_Companies_FreeWorkplaces_RW_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_WorkplaceDatas = InternalCompilerInterface.GetComponentLookup<WorkplaceData>(ref this.__TypeHandle.__Game_Prefabs_WorkplaceData_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_Deleteds = InternalCompilerInterface.GetComponentLookup<Deleted>(ref this.__TypeHandle.__Game_Common_Deleted_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_SpawnableBuildings = InternalCompilerInterface.GetComponentLookup<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_WorkProviders = InternalCompilerInterface.GetComponentLookup<WorkProvider>(ref this.__TypeHandle.__Game_Companies_WorkProvider_RO_ComponentLookup, ref base.CheckedStateRef);
				startWorkingJob.m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer();
				startWorkingJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
				startWorkingJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer();
				startWorkingJob.m_StartedWorking = this.m_StartedWorking;
				FindJobSystem.StartWorkingJob startWorkingJob2 = startWorkingJob;
				base.Dependency = startWorkingJob2.Schedule(JobHandle.CombineDependencies(jobHandle3, base.Dependency));
				this.m_TriggerSystem.AddActionBufferWriter(base.Dependency);
				this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
				this.m_WriteDeps = JobHandle.CombineDependencies(base.Dependency, this.m_WriteDeps);
			}
		}

		// Token: 0x060067DA RID: 26586 RVA: 0x00382EF8 File Offset: 0x003810F8
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x060067DB RID: 26587 RVA: 0x00382F19 File Offset: 0x00381119
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x060067DC RID: 26588 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public FindJobSystem()
		{
		}

		// Token: 0x040094CE RID: 38094
		private const int UPDATE_INTERVAL = 16;

		// Token: 0x040094CF RID: 38095
		private EntityQuery m_JobSeekerQuery;

		// Token: 0x040094D0 RID: 38096
		private EntityQuery m_ResultsQuery;

		// Token: 0x040094D1 RID: 38097
		private EntityQuery m_FreeQuery;

		// Token: 0x040094D2 RID: 38098
		private EntityQuery m_TripPriorityParametersQuery;

		// Token: 0x040094D3 RID: 38099
		private SimulationSystem m_SimulationSystem;

		// Token: 0x040094D4 RID: 38100
		private TriggerSystem m_TriggerSystem;

		// Token: 0x040094D5 RID: 38101
		private CountHouseholdDataSystem m_CountHouseholdDataSystem;

		// Token: 0x040094D6 RID: 38102
		private PathfindSetupSystem m_PathfindSetupSystem;

		// Token: 0x040094D7 RID: 38103
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x040094D8 RID: 38104
		private NativeArray<int> m_FreeCache;

		// Token: 0x040094D9 RID: 38105
		[DebugWatchValue]
		private NativeValue<int> m_StartedWorking;

		// Token: 0x040094DA RID: 38106
		[DebugWatchDeps]
		private JobHandle m_WriteDeps;

		// Token: 0x040094DB RID: 38107
		private FindJobSystem.TypeHandle __TypeHandle;

		// Token: 0x0200150E RID: 5390
		[BurstCompile]
		private struct CalculateFreeWorkplaceJob : IJob
		{
			// Token: 0x060067DD RID: 26589 RVA: 0x00382F40 File Offset: 0x00381140
			public void Execute()
			{
				for (int i = 0; i < this.m_FreeCache.Length; i++)
				{
					this.m_FreeCache[i] = 0;
				}
				for (int j = 0; j < this.m_FreeWorkplaces.Length; j++)
				{
					FreeWorkplaces freeWorkplaces = this.m_FreeWorkplaces[j];
					ref NativeArray<int> ptr = ref this.m_FreeCache;
					ptr[0] = ptr[0] + (int)freeWorkplaces.m_Uneducated;
					ptr = ref this.m_FreeCache;
					ptr[1] = ptr[1] + (int)freeWorkplaces.m_PoorlyEducated;
					ptr = ref this.m_FreeCache;
					ptr[2] = ptr[2] + (int)freeWorkplaces.m_Educated;
					ptr = ref this.m_FreeCache;
					ptr[3] = ptr[3] + (int)freeWorkplaces.m_WellEducated;
					ptr = ref this.m_FreeCache;
					ptr[4] = ptr[4] + (int)freeWorkplaces.m_HighlyEducated;
				}
			}

			// Token: 0x040094DC RID: 38108
			[ReadOnly]
			public NativeList<FreeWorkplaces> m_FreeWorkplaces;

			// Token: 0x040094DD RID: 38109
			public NativeArray<int> m_FreeCache;
		}

		// Token: 0x0200150F RID: 5391
		[BurstCompile]
		private struct FindJobJob : IJobChunk
		{
            public CustomEventData m_CustomEventData;
            // Token: 0x060067DE RID: 26590 RVA: 0x00383028 File Offset: 0x00381228
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Owner> nativeArray2 = chunk.GetNativeArray<Owner>(ref this.m_OwnerType);
				NativeArray<JobSeeker> nativeArray3 = chunk.GetNativeArray<JobSeeker>(ref this.m_JobSeekerType);
				NativeArray<CurrentBuilding> nativeArray4 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity owner = nativeArray2[i].m_Owner;
					if (this.m_Deleteds.HasComponent(owner) || !this.m_CitizenDatas.HasComponent(owner))
					{
						this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, nativeArray[i], default(Deleted));
					}
					else
					{
						Entity household = this.m_HouseholdMembers[owner].m_Household;
						Citizen citizen = this.m_CitizenDatas[owner];
						Entity entity = Entity.Null;
						if (this.m_PropertyRenters.HasComponent(household))
						{
							entity = this.m_PropertyRenters[household].m_Property;
						}
						else if (chunk.Has<CurrentBuilding>(ref this.m_CurrentBuildingType))
						{
							if ((citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None)
							{
								entity = nativeArray4[i].m_CurrentBuilding;
							}
						}
						else if (this.m_HomelessHouseholds.HasComponent(household))
						{
							entity = this.m_HomelessHouseholds[household].m_TempHome;
						}
						Entity entity2 = nativeArray[i];
						if (entity != Entity.Null)
						{
							int level = (int)nativeArray3[i].m_Level;
							int num = level;
							int num2 = -1;
							bool flag = this.m_Workers.HasComponent(owner) && this.m_OutsideConnections.HasComponent(this.m_Workers[owner].m_Workplace);
							if (this.m_Workers.HasComponent(owner) && !flag)
							{
								num2 = (int)this.m_Workers[owner].m_Level;
							}
							if (num2 >= 0 && num > level && num <= num2)
							{
								this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(unfilteredChunkIndex, owner, false);
								this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
							}
							else
							{
								while (num > num2 && this.m_FreeCache[num] <= 0)
								{
									num--;
								}
								if (num == -1)
								{
									this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(unfilteredChunkIndex, owner, false);
									this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
								}
								else
								{
									float num3 = (float)this.m_FreeCache[num];
									float num4 = (float)this.m_EmployableByEducation[num] / num3;
									if (num2 >= 0 && random.NextFloat(num4) > 2f)
									{
										this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(unfilteredChunkIndex, owner, false);
										this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
									}
									else
									{
										this.m_CommandBuffer.AddComponent<PathInformation>(unfilteredChunkIndex, entity2, new PathInformation
										{
											m_State = PathFlags.Pending
										});
										Household household2 = this.m_Households[household];
										DynamicBuffer<HouseholdCitizen> dynamicBuffer = this.m_HouseholdCitizens[household];
										PathMethod pathMethod = PathMethod.Pedestrian | PathMethod.PublicTransportDay | PathMethod.PublicTransportNight;
										if (CitizenUtils.HouseholdHasCar(household, this.m_OwnedVehicles, this.m_PersonalCars))
										{
											pathMethod |= PathMethod.Road | PathMethod.MediumRoad;
										}
										PathfindParameters pathfindParameters = new PathfindParameters
										{
											m_MaxSpeed = 111.111115f,
											m_WalkSpeed = 1.6666667f,
											m_Weights = CitizenUtils.GetPathfindWeights(citizen, household2, dynamicBuffer.Length),
											m_Methods = pathMethod,
											m_MaxCost = this.m_TripPriorityParameters.GetMaxCost(this.m_TripPriorityParameters.m_PriorityGoingToWork) * 1.1f,
											m_PathfindFlags = (PathfindFlags.Simplified | PathfindFlags.IgnorePath)
										};
										SetupQueueTarget setupQueueTarget = new SetupQueueTarget
										{
											m_Type = SetupTargetType.CurrentLocation,
											m_Methods = PathMethod.Pedestrian
										};
										SetupQueueTarget setupQueueTarget2 = setupQueueTarget;
                                        bool removeOvereducationPenalty = m_CustomEventData.RemoveOvereducationPenalty(ref random);
                                        setupQueueTarget = new SetupQueueTarget
										{
											m_Type = SetupTargetType.JobSeekerTo,
											m_Methods = PathMethod.Pedestrian,
											m_Value = level + 5 * (num + 1),
                                            m_Value2 = flag ? 0f : removeOvereducationPenalty ? 2f : num4
                                            //m_Value2 = (flag ? 0f : num4)
										};
										SetupQueueTarget setupQueueTarget3 = setupQueueTarget;
										if (nativeArray3[i].m_Outside > 0)
										{
											setupQueueTarget3.m_Flags |= SetupTargetFlags.Export;
										}
										if (flag)
										{
											setupQueueTarget3.m_Flags |= SetupTargetFlags.Import;
										}
										PathUtils.UpdateOwnedVehicleMethods(household, ref this.m_OwnedVehicles, ref pathfindParameters, ref setupQueueTarget2, ref setupQueueTarget3);
										SetupQueueItem setupQueueItem = new SetupQueueItem(entity2, pathfindParameters, setupQueueTarget2, setupQueueTarget3);
										this.m_PathfindQueue.Enqueue(setupQueueItem);
									}
								}
							}
						}
						else
						{
							this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(unfilteredChunkIndex, owner, false);
							this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
						}
					}
				}
			}

			// Token: 0x060067DF RID: 26591 RVA: 0x003834C2 File Offset: 0x003816C2
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040094DE RID: 38110
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040094DF RID: 38111
			[ReadOnly]
			public ComponentTypeHandle<Owner> m_OwnerType;

			// Token: 0x040094E0 RID: 38112
			[ReadOnly]
			public ComponentTypeHandle<JobSeeker> m_JobSeekerType;

			// Token: 0x040094E1 RID: 38113
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x040094E2 RID: 38114
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x040094E3 RID: 38115
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x040094E4 RID: 38116
			[ReadOnly]
			public ComponentLookup<Citizen> m_CitizenDatas;

			// Token: 0x040094E5 RID: 38117
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x040094E6 RID: 38118
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x040094E7 RID: 38119
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

			// Token: 0x040094E8 RID: 38120
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x040094E9 RID: 38121
			[ReadOnly]
			public ComponentLookup<Deleted> m_Deleteds;

			// Token: 0x040094EA RID: 38122
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x040094EB RID: 38123
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x040094EC RID: 38124
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCars;

			// Token: 0x040094ED RID: 38125
			public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;

			// Token: 0x040094EE RID: 38126
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040094EF RID: 38127
			[ReadOnly]
			public NativeArray<int> m_FreeCache;

			// Token: 0x040094F0 RID: 38128
			[ReadOnly]
			public NativeArray<int> m_EmployableByEducation;

			// Token: 0x040094F1 RID: 38129
			public RandomSeed m_RandomSeed;

			// Token: 0x040094F2 RID: 38130
			[ReadOnly]
			public TripPriorityParametersData m_TripPriorityParameters;
		}

		// Token: 0x02001510 RID: 5392
		[BurstCompile]
		private struct StartWorkingJob : IJob
		{
			// Token: 0x060067E0 RID: 26592 RVA: 0x003834D0 File Offset: 0x003816D0
			public void Execute()
			{
				int num = 0;
				for (int i = 0; i < this.m_Chunks.Length; i++)
				{
					ArchetypeChunk archetypeChunk = this.m_Chunks[i];
					NativeArray<Owner> nativeArray = archetypeChunk.GetNativeArray<Owner>(ref this.m_OwnerType);
					NativeArray<PathInformation> nativeArray2 = archetypeChunk.GetNativeArray<PathInformation>(ref this.m_PathInfoType);
					NativeArray<Entity> nativeArray3 = archetypeChunk.GetNativeArray(this.m_EntityType);
					NativeArray<JobSeeker> nativeArray4 = archetypeChunk.GetNativeArray<JobSeeker>(ref this.m_JobSeekerType);
					for (int j = 0; j < nativeArray3.Length; j++)
					{
						if ((nativeArray2[j].m_State & PathFlags.Pending) == (PathFlags)0)
						{
							Entity entity = nativeArray3[j];
							Entity owner = nativeArray[j].m_Owner;
							if (this.m_Citizens.HasComponent(owner) && !this.m_Deleteds.HasComponent(owner))
							{
								Entity destination = nativeArray2[j].m_Destination;
								if (this.m_Prefabs.HasComponent(destination) && this.m_EmployeeBuffers.HasBuffer(destination))
								{
									DynamicBuffer<Employee> dynamicBuffer = this.m_EmployeeBuffers[destination];
									WorkProvider workProvider = this.m_WorkProviders[destination];
									Entity entity2 = (this.m_PropertyRenters.HasComponent(destination) ? this.m_PropertyRenters[destination].m_Property : destination);
									Entity prefab = this.m_Prefabs[entity2].m_Prefab;
									int num2 = (int)(this.m_SpawnableBuildings.HasComponent(prefab) ? this.m_SpawnableBuildings[prefab].m_Level : 1);
									if (this.m_Prefabs.HasComponent(destination) && (!this.m_Workers.HasComponent(owner) || destination != this.m_Workers[owner].m_Workplace))
									{
										Entity prefab2 = this.m_Prefabs[destination].m_Prefab;
										if (this.m_WorkplaceDatas.HasComponent(prefab2))
										{
											if (this.m_FreeWorkplaces.HasComponent(destination) && this.m_FreeWorkplaces[destination].Count > 0)
											{
												WorkplaceData workplaceData = this.m_WorkplaceDatas[prefab2];
												Citizen citizen = this.m_Citizens[owner];
												Workshift workshift = Workshift.Day;
												FreeWorkplaces freeWorkplaces = this.m_FreeWorkplaces[destination];
												freeWorkplaces.Refresh(dynamicBuffer, workProvider.m_MaxWorkers, workplaceData.m_Complexity, num2);
												byte level = nativeArray4[j].m_Level;
												int bestFor = freeWorkplaces.GetBestFor((int)level);
												if (bestFor >= 0)
												{
													Unity.Mathematics.Random random = new Unity.Mathematics.Random(1U + (this.m_SimulationFrame ^ (uint)citizen.m_PseudoRandom));
													float num3 = random.NextFloat();
													if (num3 < workplaceData.m_EveningShiftProbability)
													{
														workshift = Workshift.Evening;
													}
													else if (num3 < workplaceData.m_EveningShiftProbability + workplaceData.m_NightShiftProbability)
													{
														workshift = Workshift.Night;
													}
													dynamicBuffer.Add(new Employee
													{
														m_Worker = owner,
														m_Level = (byte)bestFor
													});
													if (this.m_Workers.HasComponent(owner))
													{
														this.m_CommandBuffer.RemoveComponent<Worker>(owner);
													}
													this.m_CommandBuffer.AddComponent<Worker>(owner, new Worker
													{
														m_Workplace = destination,
														m_Level = (byte)bestFor,
														m_LastCommuteTime = nativeArray2[j].m_Duration,
														m_Shift = workshift
													});
													num++;
													this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenStartedWorking, Entity.Null, owner, destination, 0f));
													freeWorkplaces.Refresh(dynamicBuffer, workProvider.m_MaxWorkers, workplaceData.m_Complexity, num2);
													this.m_FreeWorkplaces[destination] = freeWorkplaces;
												}
											}
											else if (this.m_Workers.HasComponent(owner))
											{
											}
										}
									}
								}
								else if (CitizenUtils.IsCommuter(owner, ref this.m_Citizens))
								{
									this.m_CommandBuffer.AddComponent<Deleted>(owner, default(Deleted));
									goto IL_03DA;
								}
								this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(owner, false);
							}
							this.m_CommandBuffer.AddComponent<Deleted>(entity, default(Deleted));
						}
						IL_03DA:;
					}
				}
				this.m_StartedWorking.value = this.m_StartedWorking.value + num;
			}

			// Token: 0x040094F3 RID: 38131
			[ReadOnly]
			public NativeList<ArchetypeChunk> m_Chunks;

			// Token: 0x040094F4 RID: 38132
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040094F5 RID: 38133
			[ReadOnly]
			public ComponentTypeHandle<Owner> m_OwnerType;

			// Token: 0x040094F6 RID: 38134
			[ReadOnly]
			public ComponentTypeHandle<JobSeeker> m_JobSeekerType;

			// Token: 0x040094F7 RID: 38135
			[ReadOnly]
			public ComponentTypeHandle<PathInformation> m_PathInfoType;

			// Token: 0x040094F8 RID: 38136
			[ReadOnly]
			public ComponentLookup<Citizen> m_Citizens;

			// Token: 0x040094F9 RID: 38137
			[ReadOnly]
			public ComponentLookup<Deleted> m_Deleteds;

			// Token: 0x040094FA RID: 38138
			public BufferLookup<Employee> m_EmployeeBuffers;

			// Token: 0x040094FB RID: 38139
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x040094FC RID: 38140
			[ReadOnly]
			public ComponentLookup<WorkplaceData> m_WorkplaceDatas;

			// Token: 0x040094FD RID: 38141
			public ComponentLookup<FreeWorkplaces> m_FreeWorkplaces;

			// Token: 0x040094FE RID: 38142
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x040094FF RID: 38143
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildings;

			// Token: 0x04009500 RID: 38144
			[ReadOnly]
			public ComponentLookup<WorkProvider> m_WorkProviders;

			// Token: 0x04009501 RID: 38145
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x04009502 RID: 38146
			public NativeQueue<TriggerAction> m_TriggerBuffer;

			// Token: 0x04009503 RID: 38147
			public EntityCommandBuffer m_CommandBuffer;

			// Token: 0x04009504 RID: 38148
			public uint m_SimulationFrame;

			// Token: 0x04009505 RID: 38149
			public NativeValue<int> m_StartedWorking;
		}

		// Token: 0x02001511 RID: 5393
		private struct TypeHandle
		{
			// Token: 0x060067E1 RID: 26593 RVA: 0x003838F4 File Offset: 0x00381AF4
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Agents_JobSeeker_RW_ComponentTypeHandle = state.GetComponentTypeHandle<JobSeeker>(false);
				this.__Game_Common_Owner_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Owner>(false);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Common_Deleted_RO_ComponentLookup = state.GetComponentLookup<Deleted>(true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
				this.__Game_Vehicles_PersonalCar_RO_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(true);
				this.__Game_Agents_JobSeeker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<JobSeeker>(true);
				this.__Game_Common_Owner_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Owner>(true);
				this.__Game_Pathfind_PathInformation_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PathInformation>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Companies_Employee_RW_BufferLookup = state.GetBufferLookup<Employee>(false);
				this.__Game_Companies_FreeWorkplaces_RW_ComponentLookup = state.GetComponentLookup<FreeWorkplaces>(false);
				this.__Game_Prefabs_WorkplaceData_RO_ComponentLookup = state.GetComponentLookup<WorkplaceData>(true);
				this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(true);
				this.__Game_Companies_WorkProvider_RO_ComponentLookup = state.GetComponentLookup<WorkProvider>(true);
			}

			// Token: 0x04009506 RID: 38150
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009507 RID: 38151
			public ComponentTypeHandle<JobSeeker> __Game_Agents_JobSeeker_RW_ComponentTypeHandle;

			// Token: 0x04009508 RID: 38152
			public ComponentTypeHandle<Owner> __Game_Common_Owner_RW_ComponentTypeHandle;

			// Token: 0x04009509 RID: 38153
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x0400950A RID: 38154
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x0400950B RID: 38155
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x0400950C RID: 38156
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x0400950D RID: 38157
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x0400950E RID: 38158
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x0400950F RID: 38159
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

			// Token: 0x04009510 RID: 38160
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x04009511 RID: 38161
			[ReadOnly]
			public ComponentLookup<Deleted> __Game_Common_Deleted_RO_ComponentLookup;

			// Token: 0x04009512 RID: 38162
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

			// Token: 0x04009513 RID: 38163
			[ReadOnly]
			public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;

			// Token: 0x04009514 RID: 38164
			[ReadOnly]
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RO_ComponentLookup;

			// Token: 0x04009515 RID: 38165
			[ReadOnly]
			public ComponentTypeHandle<JobSeeker> __Game_Agents_JobSeeker_RO_ComponentTypeHandle;

			// Token: 0x04009516 RID: 38166
			[ReadOnly]
			public ComponentTypeHandle<Owner> __Game_Common_Owner_RO_ComponentTypeHandle;

			// Token: 0x04009517 RID: 38167
			[ReadOnly]
			public ComponentTypeHandle<PathInformation> __Game_Pathfind_PathInformation_RO_ComponentTypeHandle;

			// Token: 0x04009518 RID: 38168
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x04009519 RID: 38169
			public BufferLookup<Employee> __Game_Companies_Employee_RW_BufferLookup;

			// Token: 0x0400951A RID: 38170
			public ComponentLookup<FreeWorkplaces> __Game_Companies_FreeWorkplaces_RW_ComponentLookup;

			// Token: 0x0400951B RID: 38171
			[ReadOnly]
			public ComponentLookup<WorkplaceData> __Game_Prefabs_WorkplaceData_RO_ComponentLookup;

			// Token: 0x0400951C RID: 38172
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;

			// Token: 0x0400951D RID: 38173
			[ReadOnly]
			public ComponentLookup<WorkProvider> __Game_Companies_WorkProvider_RO_ComponentLookup;
		}
	}
}
