using System;
using System.Runtime.CompilerServices;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;


namespace BitulaMod
{
	// Token: 0x020014E7 RID: 5351
	public partial class CitizenFindJobSystem : GameSystemBase
	{
		// Token: 0x0600672F RID: 26415 RVA: 0x0037833D File Offset: 0x0037653D
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 262144 / (CitizenFindJobSystem.kUpdatesPerDay * 16);
		}

		// Token: 0x06006730 RID: 26416 RVA: 0x00378350 File Offset: 0x00376550
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_UnemployedQuery = base.GetEntityQuery(new EntityQueryDesc[]
			{
				new EntityQueryDesc
				{
					All = new ComponentType[]
					{
						ComponentType.ReadOnly<Citizen>(),
						ComponentType.ReadOnly<HouseholdMember>()
					},
					None = new ComponentType[]
					{
						ComponentType.ReadOnly<Temp>(),
						ComponentType.ReadOnly<Worker>(),
						ComponentType.ReadOnly<Game.Citizens.Student>(),
						ComponentType.ReadOnly<HasJobSeeker>(),
						ComponentType.ReadOnly<HasSchoolSeeker>(),
						ComponentType.ReadOnly<HealthProblem>(),
						ComponentType.ReadOnly<Deleted>()
					}
				}
			});
			this.m_EmployedQuery = base.GetEntityQuery(new EntityQueryDesc[]
			{
				new EntityQueryDesc
				{
					All = new ComponentType[]
					{
						ComponentType.ReadOnly<Citizen>(),
						ComponentType.ReadOnly<HouseholdMember>(),
						ComponentType.ReadOnly<Worker>()
					},
					None = new ComponentType[]
					{
						ComponentType.ReadOnly<Temp>(),
						ComponentType.ReadOnly<Game.Citizens.Student>(),
						ComponentType.ReadOnly<HasJobSeeker>(),
						ComponentType.ReadOnly<HasSchoolSeeker>(),
						ComponentType.ReadOnly<HealthProblem>(),
						ComponentType.ReadOnly<Deleted>()
					}
				}
			});
			this.m_CitizenParametersQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<CitizenParametersData>() });
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
            this.m_CountWorkplacesSystem = base.World.GetOrCreateSystemManaged<CountWorkplacesSystem>();
            base.RequireForUpdate(this.m_CitizenParametersQuery);
			base.RequireForUpdate(this.m_UnemployedQuery);
		}

        // Token: 0x06006731 RID: 26417 RVA: 0x0037850C File Offset: 0x0037670C
        [Preserve]
		protected override void OnUpdate()
		{
            uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, CitizenFindJobSystem.kUpdatesPerDay, 16);
			CitizenFindJobSystem.CitizenFindJobJob citizenFindJobJob = default(CitizenFindJobSystem.CitizenFindJobJob);            
            citizenFindJobJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			citizenFindJobJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenFindJobJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenFindJobJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenFindJobJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			citizenFindJobJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_MovingAways = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenFindJobJob.m_HasJobSeekers = InternalCompilerInterface.GetComponentLookup<HasJobSeeker>(ref this.__TypeHandle.__Game_Agents_HasJobSeeker_RO_ComponentLookup, ref base.CheckedStateRef);

            citizenFindJobJob.m_IsUnemployedFindJob = true;
			citizenFindJobJob.m_UpdateFrameIndex = updateFrame;
			citizenFindJobJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			citizenFindJobJob.m_RandomSeed = RandomSeed.Next();
			citizenFindJobJob.m_AvailableWorkspacesByLevel = this.m_CountWorkplacesSystem.GetUnemployedWorkspaceByLevel();
			citizenFindJobJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
            citizenFindJobJob.m_CustomEventData = CustomEventData.Create(ref base.CheckedStateRef);


            CitizenFindJobSystem.CitizenFindJobJob citizenFindJobJob2 = citizenFindJobJob;
			base.Dependency = citizenFindJobJob2.ScheduleParallel(this.m_UnemployedQuery, base.Dependency);
			if (!this.m_EmployedQuery.IsEmpty && RandomSeed.Next().GetRandom((int)this.m_SimulationSystem.frameIndex).NextFloat(1f) > this.m_CitizenParametersQuery.GetSingleton<CitizenParametersData>().m_SwitchJobRate)
			{
				citizenFindJobJob = default(CitizenFindJobSystem.CitizenFindJobJob);
				citizenFindJobJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
				citizenFindJobJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
				citizenFindJobJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				citizenFindJobJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
				citizenFindJobJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
				citizenFindJobJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_MovingAways = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
				citizenFindJobJob.m_HasJobSeekers = InternalCompilerInterface.GetComponentLookup<HasJobSeeker>(ref this.__TypeHandle.__Game_Agents_HasJobSeeker_RO_ComponentLookup, ref base.CheckedStateRef);
                citizenFindJobJob.m_IsUnemployedFindJob = false;
				citizenFindJobJob.m_UpdateFrameIndex = updateFrame;
				citizenFindJobJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
				citizenFindJobJob.m_RandomSeed = RandomSeed.Next();
				citizenFindJobJob.m_AvailableWorkspacesByLevel = this.m_CountWorkplacesSystem.GetFreeWorkplaces();
				citizenFindJobJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
				citizenFindJobJob.m_CustomEventData = CustomEventData.Create(ref base.CheckedStateRef);
                CitizenFindJobSystem.CitizenFindJobJob citizenFindJobJob3 = citizenFindJobJob;
				base.Dependency = citizenFindJobJob3.ScheduleParallel(this.m_EmployedQuery, base.Dependency);
			}
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
            CustomEventData.AddProducer( ref base.CheckedStateRef, base.Dependency);
        }

		// Token: 0x06006732 RID: 26418 RVA: 0x00378940 File Offset: 0x00376B40
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x06006733 RID: 26419 RVA: 0x00378961 File Offset: 0x00376B61
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x06006734 RID: 26420 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public CitizenFindJobSystem()
		{
		}

		// Token: 0x040092A0 RID: 37536
		public static readonly int kUpdatesPerDay = 256;

		// Token: 0x040092A1 RID: 37537
		public static readonly int kJobSeekCoolDownMax = 10000;

		// Token: 0x040092A2 RID: 37538
		public static readonly int kJobSeekCoolDownMin = 5000;

		// Token: 0x040092A3 RID: 37539
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x040092A4 RID: 37540
		private EntityQuery m_UnemployedQuery;

		// Token: 0x040092A5 RID: 37541
		private EntityQuery m_EmployedQuery;

		// Token: 0x040092A6 RID: 37542
		private EntityQuery m_CitizenParametersQuery;

		// Token: 0x040092A7 RID: 37543
		private SimulationSystem m_SimulationSystem;

        // Token: 0x040092A8 RID: 37544
        private CountWorkplacesSystem m_CountWorkplacesSystem;

        // Token: 0x040092A9 RID: 37545
        private CitizenFindJobSystem.TypeHandle __TypeHandle;

        // Token: 0x020014E8 RID: 5352
        [BurstCompile]
		private struct CitizenFindJobJob : IJobChunk
		{
            public CustomEventData m_CustomEventData;
            // Token: 0x06006736 RID: 26422 RVA: 0x003789A8 File Offset: 0x00376BA8
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<CurrentBuilding> nativeArray3 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity household = this.m_HouseholdMembers[nativeArray[i]].m_Household;
					Citizen citizen = nativeArray2[i];
					CitizenAge age = citizen.GetAge();
					if (age == CitizenAge.Child || age == CitizenAge.Elderly)
					{
						citizen.m_UnemploymentTimeCounter = 0f;
						nativeArray2[i] = citizen;
					}
					else if ((ulong)this.m_HasJobSeekers[nativeArray[i]].m_LastJobSeekFrameIndex + (ulong)((long)random.NextInt(CitizenFindJobSystem.kJobSeekCoolDownMin, CitizenFindJobSystem.kJobSeekCoolDownMax)) > (ulong)this.m_SimulationFrame)
					{
						citizen.m_UnemploymentTimeCounter += 1f / (float)CitizenFindJobSystem.kUpdatesPerDay;
						nativeArray2[i] = citizen;
					}
					else if (this.m_MovingAways.HasComponent(household))
					{
						citizen.m_UnemploymentTimeCounter += 1f / (float)CitizenFindJobSystem.kUpdatesPerDay;
						nativeArray2[i] = citizen;
					}
					else
					{
						int educationLevel = citizen.GetEducationLevel();
						if (this.m_IsUnemployedFindJob)
						{
							citizen.m_UnemploymentTimeCounter += 1f / (float)CitizenFindJobSystem.kUpdatesPerDay;
							nativeArray2[i] = citizen;
							int num = this.m_AvailableWorkspacesByLevel[educationLevel];
                            if (num <= 0 || m_CustomEventData.FailedJobApplication(num, ref random))
							{
                                Entity citizenEntity = nativeArray[i];
                                if (num <= 0)
                                    m_CustomEventData.Send(citizenEntity, CustomEventType.NoJobsAvailable);
								else
                                    m_CustomEventData.Send(citizenEntity, CustomEventType.DoesntLikeAnyJobs);
                                this.m_CommandBuffer.SetComponent<HasJobSeeker>(unfilteredChunkIndex, nativeArray[i], new HasJobSeeker
								{
									m_Seeker = Entity.Null,
									m_LastJobSeekFrameIndex = this.m_SimulationFrame
								});
								goto IL_0463;
							} else {

                                int matchingEducationPositions = educationLevel == 0
                                    ? m_AvailableWorkspacesByLevel[0]
                                    : m_AvailableWorkspacesByLevel[educationLevel]
                                      - m_AvailableWorkspacesByLevel[educationLevel - 1];
                                Entity citizenEntity = nativeArray[i];
								m_CustomEventData.AddParameter(num);
                                m_CustomEventData.AddParameter(matchingEducationPositions);
                                m_CustomEventData.Send(citizenEntity, CustomEventType.StartedLookingForWork);
                            }
						}
						else
						{
							citizen.m_UnemploymentTimeCounter = 0f;
							nativeArray2[i] = citizen;
							NativeArray<Worker> nativeArray4 = chunk.GetNativeArray<Worker>(ref this.m_WorkerType);
							int num2 = (int)(this.m_OutsideConnections.HasComponent(nativeArray4[i].m_Workplace) ? 0 : nativeArray4[i].m_Level);
                            Entity workplace = nativeArray4[i].m_Workplace;
                            Entity citizenEntity = nativeArray[i];
							if (m_CustomEventData.IsFollowed(citizenEntity)) {
								if (m_CustomEventData.IsCompany(workplace)) {
									if (!m_PropertyRenters.TryGetComponent(workplace, out PropertyRenter renter) ||
										renter.m_Property == Entity.Null) {
										m_CustomEventData.Send(citizenEntity, CustomEventType.EmployerGone);
									}
								} else if (!m_CustomEventData.HasBuilding(workplace) &&
										   !m_OutsideConnections.HasComponent(workplace)) {
									m_CustomEventData.Send(citizenEntity, CustomEventType.WorkplaceGone);
								}
							}
                            if (num2 >= educationLevel)
							{
								goto IL_0463;
							}
                            int num3 = 0;
                            int highestAvailableJobLevel = -1;

                            for (int k = num2; k <= educationLevel; k++) {
                                if (this.m_AvailableWorkspacesByLevel[k] > 0) {
                                    num3 += this.m_AvailableWorkspacesByLevel[k];
                                    highestAvailableJobLevel = k;
                                }
                            }



                            //if (num3 <= 100 || num3 < random.NextInt(500))
                            if (m_CustomEventData.SkippedJobApplicationOrSameLevel(num3, num2,
								highestAvailableJobLevel, ref random)) {
								if (num3 > 0) { 
									m_CustomEventData.AddParameter(num3);
									m_CustomEventData.Send(citizenEntity, CustomEventType.StartedLookingForAnotherJob);
								}
                                if (num3 == 0)
                                    m_CustomEventData.Send(citizenEntity, CustomEventType.CantSwitchJob);
                                else if (num3 <= 100)
									m_CustomEventData.Send(citizenEntity, CustomEventType.TooFewBetterJobs);
								else
									m_CustomEventData.Send(citizenEntity, CustomEventType.DoesntWantBetterJob);

                                this.m_CommandBuffer.SetComponent<HasJobSeeker>(unfilteredChunkIndex, nativeArray[i], new HasJobSeeker
								{
									m_Seeker = Entity.Null,
									m_LastJobSeekFrameIndex = this.m_SimulationFrame
								});
								goto IL_0463;
							}


                            m_CustomEventData.AddParameter(num3);
                            m_CustomEventData.Send(citizenEntity, CustomEventType.StartedLookingForAnotherJob);


                        }
						Entity entity = Entity.Null;
						if (!this.m_TouristHouseholds.HasComponent(household) && this.m_PropertyRenters.HasComponent(household))
						{
							entity = this.m_PropertyRenters[household].m_Property;
						}
						else if (this.m_HomelessHouseholds.HasComponent(household))
						{
							entity = this.m_HomelessHouseholds[household].m_TempHome;
						}
						else if (chunk.Has<CurrentBuilding>(ref this.m_CurrentBuildingType) && (citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None)
						{
							entity = nativeArray3[i].m_CurrentBuilding;
						}
						if (entity != Entity.Null)
						{
							Entity entity2 = this.m_CommandBuffer.CreateEntity(unfilteredChunkIndex);
							this.m_CommandBuffer.AddComponent<Owner>(unfilteredChunkIndex, entity2, new Owner
							{
								m_Owner = nativeArray[i]
							});
							this.m_CommandBuffer.AddComponent<JobSeeker>(unfilteredChunkIndex, entity2, new JobSeeker
							{
								m_Level = (byte)citizen.GetEducationLevel(),
								m_Outside = (byte)(((citizen.m_State & CitizenFlags.Commuter) != CitizenFlags.None) ? 1 : 0)
							});
							this.m_CommandBuffer.AddComponent<CurrentBuilding>(unfilteredChunkIndex, entity2, new CurrentBuilding
							{
								m_CurrentBuilding = entity
							});
							this.m_CommandBuffer.SetComponentEnabled<HasJobSeeker>(unfilteredChunkIndex, nativeArray[i], true);
							this.m_CommandBuffer.SetComponent<HasJobSeeker>(unfilteredChunkIndex, nativeArray[i], new HasJobSeeker
							{
								m_Seeker = entity2,
								m_LastJobSeekFrameIndex = this.m_SimulationFrame
							});
						}
					}
					IL_0463:;
				}
			}

			// Token: 0x06006737 RID: 26423 RVA: 0x00378E2C File Offset: 0x0037702C
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040092AA RID: 37546
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040092AB RID: 37547
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x040092AC RID: 37548
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x040092AD RID: 37549
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x040092AE RID: 37550
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x040092AF RID: 37551
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x040092B0 RID: 37552
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x040092B1 RID: 37553
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x040092B2 RID: 37554
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

			// Token: 0x040092B3 RID: 37555
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAways;

			// Token: 0x040092B4 RID: 37556
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x040092B5 RID: 37557
			[ReadOnly]
			public ComponentLookup<HasJobSeeker> m_HasJobSeekers;

            // Token: 0x040092B6 RID: 37558
            [ReadOnly]
			public Workplaces m_AvailableWorkspacesByLevel;

			// Token: 0x040092B7 RID: 37559
			[ReadOnly]
			public uint m_SimulationFrame;

            // Token: 0x040092B8 RID: 37560
            [ReadOnly]
			public uint m_UpdateFrameIndex;

			// Token: 0x040092B9 RID: 37561
			[ReadOnly]
			public bool m_IsUnemployedFindJob;

			// Token: 0x040092BA RID: 37562
			[ReadOnly]
			public RandomSeed m_RandomSeed;

			// Token: 0x040092BB RID: 37563
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        }

		// Token: 0x020014E9 RID: 5353
		private struct TypeHandle
		{
			// Token: 0x06006738 RID: 26424 RVA: 0x00378E3C File Offset: 0x0037703C
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Game_Citizens_Worker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Worker>(true);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
				this.__Game_Agents_MovingAway_RO_ComponentLookup = state.GetComponentLookup<MovingAway>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Agents_HasJobSeeker_RO_ComponentLookup = state.GetComponentLookup<HasJobSeeker>(true);
            }

			// Token: 0x040092BC RID: 37564
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x040092BD RID: 37565
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x040092BE RID: 37566
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x040092BF RID: 37567
			[ReadOnly]
			public ComponentTypeHandle<Worker> __Game_Citizens_Worker_RO_ComponentTypeHandle;

			// Token: 0x040092C0 RID: 37568
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x040092C1 RID: 37569
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x040092C2 RID: 37570
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x040092C3 RID: 37571
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x040092C4 RID: 37572
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

			// Token: 0x040092C5 RID: 37573
			[ReadOnly]
			public ComponentLookup<MovingAway> __Game_Agents_MovingAway_RO_ComponentLookup;

			// Token: 0x040092C6 RID: 37574
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x040092C7 RID: 37575
			[ReadOnly]
			public ComponentLookup<HasJobSeeker> __Game_Agents_HasJobSeeker_RO_ComponentLookup;
        }
	}
}
