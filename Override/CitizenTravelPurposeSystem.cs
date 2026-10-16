using System;
using System.Runtime.CompilerServices;
using Colossal.Collections;
using Colossal.Entities;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;
using BitulaMod.Components;
using BitulaMod.SmallCity;

namespace BitulaMod
{
	// Token: 0x0200152F RID: 5423
	public partial class CitizenTravelPurposeSystem : GameSystemBase
	{
		// Token: 0x060068EB RID: 26859 RVA: 0x0035D8E5 File Offset: 0x0035BAE5
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x060068EC RID: 26860 RVA: 0x00384E40 File Offset: 0x00383040
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_CityStatisticsSystem = base.World.GetOrCreateSystemManaged<CityStatisticsSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_ArrivedGroup = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Citizen>(),
				ComponentType.ReadWrite<TravelPurpose>(),
				ComponentType.ReadWrite<TripNeeded>(),
				ComponentType.ReadOnly<CurrentBuilding>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_StuckGroup = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Citizen>(),
				ComponentType.ReadWrite<TravelPurpose>(),
				ComponentType.ReadWrite<TripNeeded>(),
				ComponentType.Exclude<CurrentTransport>(),
				ComponentType.Exclude<CurrentBuilding>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_OutsideConnectionQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<Game.Objects.OutsideConnection>(),
				ComponentType.Exclude<Game.Objects.ElectricityOutsideConnection>(),
				ComponentType.Exclude<Game.Objects.WaterPipeOutsideConnection>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_ServiceBuildingQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<CityServiceUpkeep>(),
				ComponentType.ReadWrite<Building>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Destroyed>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_EconomyParameterGroup = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			base.RequireAnyForUpdate(new EntityQuery[] { this.m_ArrivedGroup, this.m_StuckGroup });
		}

		// Token: 0x060068ED RID: 26861 RVA: 0x00385038 File Offset: 0x00383238
		[Preserve]
		protected override void OnUpdate()
		{
			NativeQueue<CitizenTravelPurposeSystem.Arrive> nativeQueue = new NativeQueue<CitizenTravelPurposeSystem.Arrive>(Allocator.TempJob);
			CitizenTravelPurposeSystem.CitizenArriveJob citizenArriveJob = default(CitizenTravelPurposeSystem.CitizenArriveJob);
			citizenArriveJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			citizenArriveJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenArriveJob.m_TravelPurposeType = InternalCompilerInterface.GetComponentTypeHandle<TravelPurpose>(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenArriveJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenArriveJob.m_ArrivedType = InternalCompilerInterface.GetComponentTypeHandle<Arrived>(ref this.__TypeHandle.__Game_Citizens_Arrived_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenArriveJob.m_BuildingData = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_Schools = InternalCompilerInterface.GetComponentLookup<Game.Buildings.School>(ref this.__TypeHandle.__Game_Buildings_School_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_WorkProviders = InternalCompilerInterface.GetComponentLookup<WorkProvider>(ref this.__TypeHandle.__Game_Companies_WorkProvider_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_Students = InternalCompilerInterface.GetComponentLookup<Game.Citizens.Student>(ref this.__TypeHandle.__Game_Citizens_Student_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_PoliceStationData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.PoliceStation>(ref this.__TypeHandle.__Game_Buildings_PoliceStation_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_PrisonData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.Prison>(ref this.__TypeHandle.__Game_Buildings_Prison_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_HospitalData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.Hospital>(ref this.__TypeHandle.__Game_Buildings_Hospital_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_DeathcareFacilityData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.DeathcareFacility>(ref this.__TypeHandle.__Game_Buildings_DeathcareFacility_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_EmergencyShelterData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.EmergencyShelter>(ref this.__TypeHandle.__Game_Buildings_EmergencyShelter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_HealthProblems = InternalCompilerInterface.GetComponentLookup<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_MovingAways = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenArriveJob.m_EconomyParameters = this.m_EconomyParameterGroup.GetSingleton<EconomyParameterData>();
			citizenArriveJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			citizenArriveJob.m_ArriveQueue = nativeQueue.AsParallelWriter();
			citizenArriveJob.m_NormalizedTime = this.m_TimeSystem.normalizedTime;
			citizenArriveJob.m_RandomSeed = RandomSeed.Next();
            citizenArriveJob.m_SmallCityShopping = SmallCity<SCShopping>.Create(ref base.CheckedStateRef, citizenArriveJob.m_CommandBuffer);
            CitizenTravelPurposeSystem.CitizenArriveJob citizenArriveJob2 = citizenArriveJob;
			base.Dependency = citizenArriveJob2.ScheduleParallel(this.m_ArrivedGroup, base.Dependency);
			SmallCity<SCShopping>.AddProducer(ref base.CheckedStateRef, base.Dependency);
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			CitizenTravelPurposeSystem.ArriveJob arriveJob = default(CitizenTravelPurposeSystem.ArriveJob);
			arriveJob.m_CitizenPresenceData = InternalCompilerInterface.GetComponentLookup<CitizenPresence>(ref this.__TypeHandle.__Game_Buildings_CitizenPresence_RW_ComponentLookup, ref base.CheckedStateRef);
			arriveJob.m_Patients = InternalCompilerInterface.GetBufferLookup<Patient>(ref this.__TypeHandle.__Game_Buildings_Patient_RW_BufferLookup, ref base.CheckedStateRef);
			arriveJob.m_Occupants = InternalCompilerInterface.GetBufferLookup<Occupant>(ref this.__TypeHandle.__Game_Buildings_Occupant_RW_BufferLookup, ref base.CheckedStateRef);
			arriveJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RW_ComponentLookup, ref base.CheckedStateRef);
			arriveJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			arriveJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			arriveJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
			JobHandle jobHandle;
			arriveJob.m_StatisticsQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out jobHandle);
			arriveJob.m_ArriveQueue = nativeQueue;
			JobHandle jobHandle2 = arriveJob.Schedule(JobHandle.CombineDependencies(base.Dependency, jobHandle));
			nativeQueue.Dispose(jobHandle2);
			this.m_CityStatisticsSystem.AddWriter(jobHandle2);
			CitizenTravelPurposeSystem.CitizenStuckJob citizenStuckJob = default(CitizenTravelPurposeSystem.CitizenStuckJob);
			citizenStuckJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			citizenStuckJob.m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenStuckJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenStuckJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenStuckJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenStuckJob.m_MovingAways = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenStuckJob.m_Buildings = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenStuckJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenStuckJob.m_RandomSeed = RandomSeed.Next();
			JobHandle jobHandle3;
			citizenStuckJob.m_ServiceBuildings = this.m_ServiceBuildingQuery.ToEntityListAsync(base.World.UpdateAllocator.ToAllocator, out jobHandle3);
			JobHandle jobHandle4;
			citizenStuckJob.m_OutsideConnections = this.m_OutsideConnectionQuery.ToEntityListAsync(base.World.UpdateAllocator.ToAllocator, out jobHandle4);
			citizenStuckJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			JobHandle jobHandle5 = citizenStuckJob.ScheduleParallel(this.m_StuckGroup, JobUtils.CombineDependencies(jobHandle4, jobHandle3, jobHandle2, base.Dependency));
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle5);
			base.Dependency = JobHandle.CombineDependencies(jobHandle2, jobHandle5);
		}

		// Token: 0x060068EE RID: 26862 RVA: 0x003855C4 File Offset: 0x003837C4
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x060068EF RID: 26863 RVA: 0x003855E5 File Offset: 0x003837E5
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x060068F0 RID: 26864 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public CitizenTravelPurposeSystem()
		{
		}

		// Token: 0x040094C7 RID: 38087
		private TimeSystem m_TimeSystem;

		// Token: 0x040094C8 RID: 38088
		private CityStatisticsSystem m_CityStatisticsSystem;

		// Token: 0x040094C9 RID: 38089
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x040094CA RID: 38090
		private EntityQuery m_ArrivedGroup;

		// Token: 0x040094CB RID: 38091
		private EntityQuery m_StuckGroup;

		// Token: 0x040094CC RID: 38092
		private EntityQuery m_EconomyParameterGroup;

		// Token: 0x040094CD RID: 38093
		private EntityQuery m_OutsideConnectionQuery;

		// Token: 0x040094CE RID: 38094
		private EntityQuery m_ServiceBuildingQuery;

		// Token: 0x040094CF RID: 38095
		private CitizenTravelPurposeSystem.TypeHandle __TypeHandle;

		// Token: 0x02001530 RID: 5424
		[BurstCompile]
		private struct CitizenArriveJob : IJobChunk
		{
            // Token: 0x060068F1 RID: 26865 RVA: 0x0038560C File Offset: 0x0038380C
            public SmallCity<SCShopping> m_SmallCityShopping;
            private bool IsSleepAllowed(Entity citizenEntity)
			{
				HouseholdMember householdMember;
				return this.m_HouseholdMembers.TryGetComponent(citizenEntity, out householdMember) && !this.m_MovingAways.HasComponent(householdMember.m_Household);
			}

			// Token: 0x060068F2 RID: 26866 RVA: 0x00385640 File Offset: 0x00383840
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<TravelPurpose> nativeArray2 = chunk.GetNativeArray<TravelPurpose>(ref this.m_TravelPurposeType);
				NativeArray<CurrentBuilding> nativeArray3 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				bool flag = chunk.Has<HealthProblem>(ref this.m_HealthProblemType);
				for (int i = 0; i < chunk.Count; i++)
				{
					bool flag2 = chunk.IsComponentEnabled<Arrived>(ref this.m_ArrivedType, i);
					Entity entity = nativeArray[i];
					TravelPurpose travelPurpose = nativeArray2[i];
					if (flag && CitizenUtils.IsDead(entity, ref this.m_HealthProblems) && travelPurpose.m_Purpose != Purpose.Deathcare && travelPurpose.m_Purpose != Purpose.InDeathcare && travelPurpose.m_Purpose != Purpose.Hospital && travelPurpose.m_Purpose != Purpose.InHospital)
					{
						this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
					}
					else if (travelPurpose.m_Purpose == Purpose.Sleeping)
					{
						Citizen citizen = this.m_Citizens[entity];
						if (!this.IsSleepAllowed(entity) || !this.IsSleepTime(entity, citizen))
						{
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							if (nativeArray3.Length != 0 && this.m_BuildingData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.WakeUp));
							}
						}
					}
					else if (travelPurpose.m_Purpose == Purpose.VisitAttractions)
					{
						if (flag2)
						{
							this.m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, entity, false);
						}
						if (random.NextInt(100) == 0)
						{
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
						}
					}
					else if (flag2)
					{
						m_SmallCityShopping.Get().init(entity, SmallCityJobsPhase.Shopping);
                        if (travelPurpose.m_Purpose == Purpose.Shopping && !m_SmallCityShopping.Get().IsEndOfShopping(entity)) {
                            continue;
                        }
                        this.m_CommandBuffer.SetComponentEnabled<Arrived>(unfilteredChunkIndex, entity, false);
						switch (travelPurpose.m_Purpose)
						{
						case Purpose.None:
						case Purpose.Shopping:
						case Purpose.Leisure:
						case Purpose.Exporting:
						case Purpose.MovingAway:
						case Purpose.Safety:
						case Purpose.Escape:
						case Purpose.Traveling:
						case Purpose.SendMail:
						case Purpose.Disappear:
						case Purpose.WaitingHome:
						case Purpose.PathFailed:
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							break;
						case Purpose.GoingHome:
							if (nativeArray3.Length != 0 && this.m_BuildingData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Resident));
							}
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							break;
						case Purpose.GoingToWork:
							if (nativeArray3.Length != 0 && this.m_BuildingData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Worker));
							}
							if (this.m_Workers.HasComponent(entity))
							{
								Entity workplace = this.m_Workers[entity].m_Workplace;
								if (this.m_WorkProviders.HasComponent(workplace))
								{
									travelPurpose.m_Purpose = Purpose.Working;
									nativeArray2[i] = travelPurpose;
								}
								else
								{
									this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
								}
							}
							break;
						case Purpose.GoingToSchool:
							if (this.m_Students.HasComponent(entity))
							{
								Entity school = this.m_Students[entity].m_School;
								if (this.m_Schools.HasComponent(school))
								{
									travelPurpose.m_Purpose = Purpose.Studying;
									nativeArray2[i] = travelPurpose;
								}
								else
								{
									this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
								}
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						case Purpose.Hospital:
							if (nativeArray3.Length != 0 && this.m_HospitalData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								travelPurpose.m_Purpose = Purpose.InHospital;
								nativeArray2[i] = travelPurpose;
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Patient));
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						case Purpose.EmergencyShelter:
							if (nativeArray3.Length != 0 && this.m_EmergencyShelterData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								travelPurpose.m_Purpose = Purpose.InEmergencyShelter;
								nativeArray2[i] = travelPurpose;
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Occupant));
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						case Purpose.GoingToJail:
							if (nativeArray3.Length != 0 && this.m_PoliceStationData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								travelPurpose.m_Purpose = Purpose.InJail;
								nativeArray2[i] = travelPurpose;
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Occupant));
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						case Purpose.GoingToPrison:
							if (nativeArray3.Length != 0 && this.m_PrisonData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								travelPurpose.m_Purpose = Purpose.InPrison;
								nativeArray2[i] = travelPurpose;
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Occupant));
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						case Purpose.Deathcare:
							if (nativeArray3.Length != 0 && this.m_DeathcareFacilityData.HasComponent(nativeArray3[i].m_CurrentBuilding))
							{
								travelPurpose.m_Purpose = Purpose.InDeathcare;
								nativeArray2[i] = travelPurpose;
								this.m_ArriveQueue.Enqueue(new CitizenTravelPurposeSystem.Arrive(entity, nativeArray3[i].m_CurrentBuilding, CitizenTravelPurposeSystem.ArriveType.Patient));
							}
							else
							{
								this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
							}
							break;
						}
					} else if (!flag2 && travelPurpose.m_Purpose == Purpose.Shopping) {
                        m_SmallCityShopping.Get().init(entity, SmallCityJobsPhase.Shopping);

                        if (m_SmallCityShopping.Get().UseSmallCityBehavior() &&
                            !m_SmallCityShopping.Get().HasShoppingStart(entity)) {

                            m_CommandBuffer.SetComponentEnabled<Arrived>(
                                unfilteredChunkIndex, entity, true);
                        }
                    }
                }
			}

			// Token: 0x060068F3 RID: 26867 RVA: 0x00385C64 File Offset: 0x00383E64
			private bool IsSleepTime(Entity citizen, Citizen citizenData)
			{
				Worker worker;
				bool flag = this.m_Workers.TryGetComponent(citizen, out worker);
				Game.Citizens.Student student;
				bool flag2 = this.m_Students.TryGetComponent(citizen, out student);
				return CitizenBehaviorSystem.IsSleepTime(citizen, citizenData, ref this.m_EconomyParameters, this.m_NormalizedTime, flag, worker, flag2, student);
			}

			// Token: 0x060068F4 RID: 26868 RVA: 0x00385CA6 File Offset: 0x00383EA6
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040094D0 RID: 38096
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040094D1 RID: 38097
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x040094D2 RID: 38098
			public ComponentTypeHandle<TravelPurpose> m_TravelPurposeType;

			// Token: 0x040094D3 RID: 38099
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x040094D4 RID: 38100
			[ReadOnly]
			public ComponentTypeHandle<Arrived> m_ArrivedType;

			// Token: 0x040094D5 RID: 38101
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x040094D6 RID: 38102
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> m_Students;

			// Token: 0x040094D7 RID: 38103
			[ReadOnly]
			public ComponentLookup<WorkProvider> m_WorkProviders;

			// Token: 0x040094D8 RID: 38104
			[ReadOnly]
			public ComponentLookup<Building> m_BuildingData;

			// Token: 0x040094D9 RID: 38105
			[ReadOnly]
			public ComponentLookup<Game.Buildings.School> m_Schools;

			// Token: 0x040094DA RID: 38106
			[ReadOnly]
			public ComponentLookup<Game.Buildings.PoliceStation> m_PoliceStationData;

			// Token: 0x040094DB RID: 38107
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Prison> m_PrisonData;

			// Token: 0x040094DC RID: 38108
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Hospital> m_HospitalData;

			// Token: 0x040094DD RID: 38109
			[ReadOnly]
			public ComponentLookup<Game.Buildings.DeathcareFacility> m_DeathcareFacilityData;

			// Token: 0x040094DE RID: 38110
			[ReadOnly]
			public ComponentLookup<Game.Buildings.EmergencyShelter> m_EmergencyShelterData;

			// Token: 0x040094DF RID: 38111
			[ReadOnly]
			public ComponentLookup<Citizen> m_Citizens;

			// Token: 0x040094E0 RID: 38112
			[ReadOnly]
			public ComponentLookup<HealthProblem> m_HealthProblems;

			// Token: 0x040094E1 RID: 38113
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x040094E2 RID: 38114
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAways;

			// Token: 0x040094E3 RID: 38115
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040094E4 RID: 38116
			public NativeQueue<CitizenTravelPurposeSystem.Arrive>.ParallelWriter m_ArriveQueue;

			// Token: 0x040094E5 RID: 38117
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x040094E6 RID: 38118
			public float m_NormalizedTime;

			// Token: 0x040094E7 RID: 38119
			public RandomSeed m_RandomSeed;
		}

		// Token: 0x02001531 RID: 5425
		private struct Arrive
		{
			// Token: 0x060068F5 RID: 26869 RVA: 0x00385CB3 File Offset: 0x00383EB3
			public Arrive(Entity citizen, Entity target, CitizenTravelPurposeSystem.ArriveType type)
			{
				this.m_Citizen = citizen;
				this.m_Target = target;
				this.m_Type = type;
			}

			// Token: 0x040094E8 RID: 38120
			public Entity m_Citizen;

			// Token: 0x040094E9 RID: 38121
			public Entity m_Target;

			// Token: 0x040094EA RID: 38122
			public CitizenTravelPurposeSystem.ArriveType m_Type;
		}

		// Token: 0x02001532 RID: 5426
		private enum ArriveType
		{
			// Token: 0x040094EC RID: 38124
			Patient,
			// Token: 0x040094ED RID: 38125
			Occupant,
			// Token: 0x040094EE RID: 38126
			Resident,
			// Token: 0x040094EF RID: 38127
			Worker,
			// Token: 0x040094F0 RID: 38128
			WakeUp
		}

		// Token: 0x02001533 RID: 5427
		[BurstCompile]
		private struct ArriveJob : IJob
		{
			// Token: 0x060068F6 RID: 26870 RVA: 0x00385CCC File Offset: 0x00383ECC
			private void SetPresent(CitizenTravelPurposeSystem.Arrive arrive)
			{
				if (this.m_CitizenPresenceData.HasComponent(arrive.m_Target))
				{
					CitizenPresence citizenPresence = this.m_CitizenPresenceData[arrive.m_Target];
					citizenPresence.m_Delta = (sbyte)math.min(127, (int)(citizenPresence.m_Delta + 1));
					this.m_CitizenPresenceData[arrive.m_Target] = citizenPresence;
				}
			}

			// Token: 0x060068F7 RID: 26871 RVA: 0x00385D28 File Offset: 0x00383F28
			public void Execute()
			{
				int count = this.m_ArriveQueue.Count;
				for (int i = 0; i < count; i++)
				{
					CitizenTravelPurposeSystem.Arrive arrive = this.m_ArriveQueue.Dequeue();
					switch (arrive.m_Type)
					{
					case CitizenTravelPurposeSystem.ArriveType.Patient:
						if (this.m_Patients.HasBuffer(arrive.m_Target))
						{
							CollectionUtils.TryAddUniqueValue<Patient>(this.m_Patients[arrive.m_Target], new Patient(arrive.m_Citizen));
						}
						break;
					case CitizenTravelPurposeSystem.ArriveType.Occupant:
						if (this.m_Occupants.HasBuffer(arrive.m_Target))
						{
							CollectionUtils.TryAddUniqueValue<Occupant>(this.m_Occupants[arrive.m_Target], new Occupant(arrive.m_Citizen));
						}
						break;
					case CitizenTravelPurposeSystem.ArriveType.Resident:
					{
						Entity household = this.m_HouseholdMembers[arrive.m_Citizen].m_Household;
						if (this.m_PropertyRenters.HasComponent(household) && this.m_PropertyRenters[household].m_Property == arrive.m_Target)
						{
							Household household2 = this.m_Households[household];
							if (this.m_HouseholdCitizens.HasBuffer(household) && (household2.m_Flags & HouseholdFlags.MovedIn) == HouseholdFlags.None)
							{
								this.m_StatisticsQueue.Enqueue(new StatisticsEvent
								{
									m_Statistic = StatisticType.CitizensMovedIn,
									m_Change = (float)this.m_HouseholdCitizens[household].Length
								});
							}
							household2.m_Flags |= HouseholdFlags.MovedIn;
							this.m_Households[household] = household2;
						}
						this.SetPresent(arrive);
						break;
					}
					case CitizenTravelPurposeSystem.ArriveType.Worker:
					case CitizenTravelPurposeSystem.ArriveType.WakeUp:
						this.SetPresent(arrive);
						break;
					}
				}
			}

			// Token: 0x040094F1 RID: 38129
			public ComponentLookup<CitizenPresence> m_CitizenPresenceData;

			// Token: 0x040094F2 RID: 38130
			public BufferLookup<Patient> m_Patients;

			// Token: 0x040094F3 RID: 38131
			public BufferLookup<Occupant> m_Occupants;

			// Token: 0x040094F4 RID: 38132
			public ComponentLookup<Household> m_Households;

			// Token: 0x040094F5 RID: 38133
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x040094F6 RID: 38134
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x040094F7 RID: 38135
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x040094F8 RID: 38136
			public NativeQueue<StatisticsEvent> m_StatisticsQueue;

			// Token: 0x040094F9 RID: 38137
			public NativeQueue<CitizenTravelPurposeSystem.Arrive> m_ArriveQueue;
		}

		// Token: 0x02001534 RID: 5428
		[BurstCompile]
		private struct CitizenStuckJob : IJobChunk
		{
			// Token: 0x060068F8 RID: 26872 RVA: 0x00385ED4 File Offset: 0x003840D4
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<HouseholdMember> nativeArray2 = chunk.GetNativeArray<HouseholdMember>(ref this.m_HouseholdMemberType);
				NativeArray<HealthProblem> nativeArray3 = chunk.GetNativeArray<HealthProblem>(ref this.m_HealthProblemType);
				NativeArray<Citizen> nativeArray4 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				if (nativeArray2.Length < chunk.Count)
				{
					return;
				}
				if (this.m_OutsideConnections.Length == 0)
				{
					return;
				}
				for (int i = 0; i < chunk.Count; i++)
				{
					Entity entity = nativeArray[i];
					Entity household = nativeArray2[i].m_Household;
					bool flag = (this.m_Households[household].m_Flags & HouseholdFlags.MovedIn) != HouseholdFlags.None && !this.m_MovingAways.HasComponent(household);
					HealthProblem healthProblem;
					if (CollectionUtils.TryGet<HealthProblem>(nativeArray3, i, out healthProblem) && (healthProblem.m_Flags & HealthProblemFlags.Dead) != HealthProblemFlags.None)
					{
						this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, nativeArray[i]);
					}
					else
					{
						Entity entity2 = Entity.Null;
						Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom((1 + i) * (entity.Index + 1));
						if (flag)
						{
							if (this.m_PropertyRenters.HasComponent(household))
							{
								entity2 = this.m_PropertyRenters[household].m_Property;
							}
							if (entity2 == Entity.Null && this.m_ServiceBuildings.Length > 0)
							{
								int num = 0;
								do
								{
									num++;
									entity2 = this.m_ServiceBuildings[random.NextInt(this.m_ServiceBuildings.Length)];
								}
								while ((!this.m_Buildings.HasComponent(entity2) || this.m_Buildings[entity2].m_RoadEdge == Entity.Null) && num < 10);
							}
							if (!this.m_Buildings.HasComponent(entity2) || this.m_Buildings[entity2].m_RoadEdge == Entity.Null)
							{
								this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, nativeArray[i]);
							}
						}
						else
						{
							entity2 = this.m_OutsideConnections[random.NextInt(this.m_OutsideConnections.Length)];
						}
						if (entity2 != Entity.Null)
						{
							this.m_CommandBuffer.AddComponent<CurrentBuilding>(unfilteredChunkIndex, nativeArray[i], new CurrentBuilding
							{
								m_CurrentBuilding = entity2
							});
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, nativeArray[i]);
							Citizen citizen = nativeArray4[i];
							citizen.m_PenaltyCounter = byte.MaxValue;
							nativeArray4[i] = citizen;
						}
					}
				}
			}

			// Token: 0x060068F9 RID: 26873 RVA: 0x00386158 File Offset: 0x00384358
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040094FA RID: 38138
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040094FB RID: 38139
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			// Token: 0x040094FC RID: 38140
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x040094FD RID: 38141
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x040094FE RID: 38142
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x040094FF RID: 38143
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAways;

			// Token: 0x04009500 RID: 38144
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x04009501 RID: 38145
			[ReadOnly]
			public ComponentLookup<Building> m_Buildings;

			// Token: 0x04009502 RID: 38146
			[ReadOnly]
			public NativeList<Entity> m_OutsideConnections;

			// Token: 0x04009503 RID: 38147
			[ReadOnly]
			public NativeList<Entity> m_ServiceBuildings;

			// Token: 0x04009504 RID: 38148
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x04009505 RID: 38149
			public RandomSeed m_RandomSeed;
		}

		// Token: 0x02001535 RID: 5429
		private struct TypeHandle
		{
			// Token: 0x060068FA RID: 26874 RVA: 0x00386168 File Offset: 0x00384368
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Game_Citizens_TravelPurpose_RW_ComponentTypeHandle = state.GetComponentTypeHandle<TravelPurpose>(false);
				this.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(true);
				this.__Game_Citizens_Arrived_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Arrived>(true);
				this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
				this.__Game_Buildings_School_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.School>(true);
				this.__Game_Companies_WorkProvider_RO_ComponentLookup = state.GetComponentLookup<WorkProvider>(true);
				this.__Game_Citizens_Student_RO_ComponentLookup = state.GetComponentLookup<Game.Citizens.Student>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Buildings_PoliceStation_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.PoliceStation>(true);
				this.__Game_Buildings_Prison_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.Prison>(true);
				this.__Game_Buildings_Hospital_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.Hospital>(true);
				this.__Game_Buildings_DeathcareFacility_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.DeathcareFacility>(true);
				this.__Game_Buildings_EmergencyShelter_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.EmergencyShelter>(true);
				this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
				this.__Game_Citizens_HealthProblem_RO_ComponentLookup = state.GetComponentLookup<HealthProblem>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Agents_MovingAway_RO_ComponentLookup = state.GetComponentLookup<MovingAway>(true);
				this.__Game_Buildings_CitizenPresence_RW_ComponentLookup = state.GetComponentLookup<CitizenPresence>(false);
				this.__Game_Buildings_Patient_RW_BufferLookup = state.GetBufferLookup<Patient>(false);
				this.__Game_Buildings_Occupant_RW_BufferLookup = state.GetBufferLookup<Occupant>(false);
				this.__Game_Citizens_Household_RW_ComponentLookup = state.GetComponentLookup<Household>(false);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(true);
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
			}

			// Token: 0x04009506 RID: 38150
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009507 RID: 38151
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x04009508 RID: 38152
			public ComponentTypeHandle<TravelPurpose> __Game_Citizens_TravelPurpose_RW_ComponentTypeHandle;

			// Token: 0x04009509 RID: 38153
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentTypeHandle;

			// Token: 0x0400950A RID: 38154
			[ReadOnly]
			public ComponentTypeHandle<Arrived> __Game_Citizens_Arrived_RO_ComponentTypeHandle;

			// Token: 0x0400950B RID: 38155
			[ReadOnly]
			public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

			// Token: 0x0400950C RID: 38156
			[ReadOnly]
			public ComponentLookup<Game.Buildings.School> __Game_Buildings_School_RO_ComponentLookup;

			// Token: 0x0400950D RID: 38157
			[ReadOnly]
			public ComponentLookup<WorkProvider> __Game_Companies_WorkProvider_RO_ComponentLookup;

			// Token: 0x0400950E RID: 38158
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> __Game_Citizens_Student_RO_ComponentLookup;

			// Token: 0x0400950F RID: 38159
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x04009510 RID: 38160
			[ReadOnly]
			public ComponentLookup<Game.Buildings.PoliceStation> __Game_Buildings_PoliceStation_RO_ComponentLookup;

			// Token: 0x04009511 RID: 38161
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Prison> __Game_Buildings_Prison_RO_ComponentLookup;

			// Token: 0x04009512 RID: 38162
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Hospital> __Game_Buildings_Hospital_RO_ComponentLookup;

			// Token: 0x04009513 RID: 38163
			[ReadOnly]
			public ComponentLookup<Game.Buildings.DeathcareFacility> __Game_Buildings_DeathcareFacility_RO_ComponentLookup;

			// Token: 0x04009514 RID: 38164
			[ReadOnly]
			public ComponentLookup<Game.Buildings.EmergencyShelter> __Game_Buildings_EmergencyShelter_RO_ComponentLookup;

			// Token: 0x04009515 RID: 38165
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x04009516 RID: 38166
			[ReadOnly]
			public ComponentLookup<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentLookup;

			// Token: 0x04009517 RID: 38167
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x04009518 RID: 38168
			[ReadOnly]
			public ComponentLookup<MovingAway> __Game_Agents_MovingAway_RO_ComponentLookup;

			// Token: 0x04009519 RID: 38169
			public ComponentLookup<CitizenPresence> __Game_Buildings_CitizenPresence_RW_ComponentLookup;

			// Token: 0x0400951A RID: 38170
			public BufferLookup<Patient> __Game_Buildings_Patient_RW_BufferLookup;

			// Token: 0x0400951B RID: 38171
			public BufferLookup<Occupant> __Game_Buildings_Occupant_RW_BufferLookup;

			// Token: 0x0400951C RID: 38172
			public ComponentLookup<Household> __Game_Citizens_Household_RW_ComponentLookup;

			// Token: 0x0400951D RID: 38173
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x0400951E RID: 38174
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;

			// Token: 0x0400951F RID: 38175
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			// Token: 0x04009520 RID: 38176
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x04009521 RID: 38177
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;
		}
	}
}
