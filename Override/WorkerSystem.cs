using System;
using System.Runtime.CompilerServices;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using Game.Triggers;
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
	// Token: 0x02001556 RID: 5462
	public partial class WorkerSystem : GameSystemBase
	{
		// Token: 0x060068C5 RID: 26821 RVA: 0x003561C9 File Offset: 0x003543C9
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x060068C6 RID: 26822 RVA: 0x003920E4 File Offset: 0x003902E4
		public static float GetWorkOffset(Citizen citizen)
		{
			return (float)(-10922 + citizen.GetPseudoRandom(CitizenPseudoRandom.WorkOffset).NextInt(21845)) / 262144f;
		}

		// Token: 0x060068C7 RID: 26823 RVA: 0x00392118 File Offset: 0x00390318
		public static bool IsTodayOffDay(Citizen citizen, ref EconomyParameterData economyParameters, uint frame, TimeData timeData, int population)
		{
			int num = math.min(40, Mathf.RoundToInt(100f / math.max(1f, math.sqrt(economyParameters.m_TrafficReduction * (float)population))));
			int day = TimeSystem.GetDay(frame, timeData);
			return Unity.Mathematics.Random.CreateFromIndex((uint)((int)citizen.m_PseudoRandom + day)).NextInt(100) > num;
		}

		// Token: 0x060068C8 RID: 26824 RVA: 0x00392178 File Offset: 0x00390378
		public static bool IsTimeToWork(Citizen citizen, Worker worker, ref EconomyParameterData economyParameters, float timeOfDay)
		{
			float2 timeToWork = WorkerSystem.GetTimeToWork(citizen, worker, ref economyParameters, true);
			if (timeToWork.x >= timeToWork.y)
			{
				return timeOfDay >= timeToWork.x || timeOfDay <= timeToWork.y;
			}
			return timeOfDay >= timeToWork.x && timeOfDay <= timeToWork.y;
		}

		// Token: 0x060068C9 RID: 26825 RVA: 0x003921CC File Offset: 0x003903CC
		public static float2 GetTimeToWork(Citizen citizen, Worker worker, ref EconomyParameterData economyParameters, bool includeCommute)
		{
			float num = WorkerSystem.GetWorkOffset(citizen);
			if (worker.m_Shift == Workshift.Evening)
			{
				num += 0.33f;
			}
			else if (worker.m_Shift == Workshift.Night)
			{
				num += 0.67f;
			}
			float num2 = math.frac((float)Mathf.RoundToInt(24f * (economyParameters.m_WorkDayStart + num)) / 24f);
			float num3 = math.frac((float)Mathf.RoundToInt(24f * (economyParameters.m_WorkDayEnd + num)) / 24f);
			float num4 = 0f;
			if (includeCommute)
			{
				num4 = 60f * worker.m_LastCommuteTime;
				if (num4 < 60f)
				{
					num4 = 40000f;
				}
				num4 /= 262144f;
			}
			return new float2(math.frac(num2 - num4), num3);
		}

		// Token: 0x060068CA RID: 26826 RVA: 0x0039227C File Offset: 0x0039047C
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_CitizenBehaviorSystem = base.World.GetOrCreateSystemManaged<CitizenBehaviorSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
			this.m_WorkerQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Worker>(),
				ComponentType.ReadOnly<Citizen>(),
				ComponentType.ReadOnly<TravelPurpose>(),
				ComponentType.ReadOnly<CurrentBuilding>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_GotoWorkQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Worker>(),
				ComponentType.ReadOnly<Citizen>(),
				ComponentType.ReadOnly<CurrentBuilding>(),
				ComponentType.Exclude<TravelPurpose>(),
				ComponentType.Exclude<HealthProblem>(),
				ComponentType.Exclude<ResourceBuyer>(),
				ComponentType.ReadWrite<TripNeeded>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_TimeDataQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TimeData>() });
			this.m_PopulationQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<Population>() });
			base.RequireAnyForUpdate(new EntityQuery[] { this.m_GotoWorkQuery, this.m_WorkerQuery });
			base.RequireForUpdate(this.m_EconomyParameterQuery);
		}

		// Token: 0x060068CB RID: 26827 RVA: 0x0039244C File Offset: 0x0039064C
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			WorkerSystem.GoToWorkJob goToWorkJob = default(WorkerSystem.GoToWorkJob);
			goToWorkJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_TripType = InternalCompilerInterface.GetBufferTypeHandle<TripNeeded>(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			goToWorkJob.m_Buildings = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_CarKeepers = InternalCompilerInterface.GetComponentLookup<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_Properties = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_Purposes = InternalCompilerInterface.GetComponentLookup<TravelPurpose>(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_Attendings = InternalCompilerInterface.GetComponentLookup<AttendingMeeting>(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_PopulationData = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
			goToWorkJob.m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer().AsParallelWriter();
			goToWorkJob.m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
			goToWorkJob.m_TimeOfDay = this.m_TimeSystem.normalizedTime;
			goToWorkJob.m_UpdateFrameIndex = updateFrameWithInterval;
			goToWorkJob.m_Frame = this.m_SimulationSystem.frameIndex;
			goToWorkJob.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			goToWorkJob.m_PopulationEntity = this.m_PopulationQuery.GetSingletonEntity();
            goToWorkJob.m_CustomEventData = CustomEventData.Create(ref base.CheckedStateRef);
            JobHandle jobHandle;
			goToWorkJob.m_CarReserverQueue = this.m_CitizenBehaviorSystem.GetCarReserveQueue(out jobHandle);
			goToWorkJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			JobHandle jobHandle2 = goToWorkJob.ScheduleParallel(this.m_GotoWorkQuery, JobHandle.CombineDependencies(base.Dependency, jobHandle));
            CustomEventData.AddProducer(ref base.CheckedStateRef, jobHandle2);
            this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
			this.m_CitizenBehaviorSystem.AddCarReserveWriter(jobHandle2);
			this.m_TriggerSystem.AddActionBufferWriter(jobHandle2);
			WorkerSystem.WorkJob workJob = default(WorkerSystem.WorkJob);
			workJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			workJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			workJob.m_PurposeType = InternalCompilerInterface.GetComponentTypeHandle<TravelPurpose>(ref this.__TypeHandle.__Game_Citizens_TravelPurpose_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			workJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			workJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			workJob.m_Attendings = InternalCompilerInterface.GetComponentLookup<AttendingMeeting>(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentLookup, ref base.CheckedStateRef);
			workJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			workJob.m_Workplaces = InternalCompilerInterface.GetComponentLookup<WorkProvider>(ref this.__TypeHandle.__Game_Companies_WorkProvider_RO_ComponentLookup, ref base.CheckedStateRef);
			workJob.m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer().AsParallelWriter();
			workJob.m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
			workJob.m_UpdateFrameIndex = updateFrameWithInterval;
			workJob.m_TimeOfDay = this.m_TimeSystem.normalizedTime;
			workJob.m_Frame = this.m_SimulationSystem.frameIndex;
			workJob.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			workJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			JobHandle jobHandle3 = workJob.ScheduleParallel(this.m_WorkerQuery, JobHandle.CombineDependencies(base.Dependency, jobHandle2));
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle3);
			this.m_TriggerSystem.AddActionBufferWriter(jobHandle3);
			base.Dependency = jobHandle3;
		}

		// Token: 0x060068CC RID: 26828 RVA: 0x00392890 File Offset: 0x00390A90
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x060068CD RID: 26829 RVA: 0x003928B1 File Offset: 0x00390AB1
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x060068CE RID: 26830 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public WorkerSystem()
		{
		}

		// Token: 0x04009927 RID: 39207
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x04009928 RID: 39208
		private TimeSystem m_TimeSystem;

		// Token: 0x04009929 RID: 39209
		private CitizenBehaviorSystem m_CitizenBehaviorSystem;

		// Token: 0x0400992A RID: 39210
		private EntityQuery m_EconomyParameterQuery;

		// Token: 0x0400992B RID: 39211
		private EntityQuery m_GotoWorkQuery;

		// Token: 0x0400992C RID: 39212
		private EntityQuery m_WorkerQuery;

		// Token: 0x0400992D RID: 39213
		private EntityQuery m_TimeDataQuery;

		// Token: 0x0400992E RID: 39214
		private EntityQuery m_PopulationQuery;

		// Token: 0x0400992F RID: 39215
		private SimulationSystem m_SimulationSystem;

		// Token: 0x04009930 RID: 39216
		private TriggerSystem m_TriggerSystem;

		// Token: 0x04009931 RID: 39217
		private WorkerSystem.TypeHandle __TypeHandle;

		// Token: 0x02001557 RID: 5463
		[BurstCompile]
		private struct GoToWorkJob : IJobChunk
		{
            // Token: 0x060068CF RID: 26831 RVA: 0x003928D8 File Offset: 0x00390AD8
            public CustomEventData m_CustomEventData;
            public static int GetRemainingOffDays(Citizen citizen,
    ref EconomyParameterData economyParameters,
    uint frame, TimeData timeData, int population) {

                int num = math.min(
                    40,
                    Mathf.RoundToInt(
                        100f / math.max(
                            1f,
                            math.sqrt(economyParameters.m_TrafficReduction * (float)population))));

                int currentDay = TimeSystem.GetDay(frame, timeData);
                int offDays = 0;

                for (int day = currentDay; ; day++) {
                    bool isOffDay =
                        Unity.Mathematics.Random.CreateFromIndex(
                            (uint)((int)citizen.m_PseudoRandom + day))
                        .NextInt(100) > num;

                    if (!isOffDay)
                        break;

                    offDays++;
                }

                return offDays;
            }
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<Worker> nativeArray3 = chunk.GetNativeArray<Worker>(ref this.m_WorkerType);
				NativeArray<CurrentBuilding> nativeArray4 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripType);
				int population = this.m_PopulationData[this.m_PopulationEntity].m_Population;
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					Citizen citizen = nativeArray2[i];
                    Entity workplace = nativeArray3[i].m_Workplace;

                    bool invalidWorkplace =
                        !this.m_Properties.HasComponent(workplace) &&
                        !this.m_Buildings.HasComponent(workplace) &&
                        !this.m_OutsideConnections.HasComponent(workplace);
                    if (invalidWorkplace && this.m_CustomEventData.IsFollowed(entity)) {
                        bool isOffDay = WorkerSystem.IsTodayOffDay(
                            citizen,
                            ref this.m_EconomyParameters,
                            this.m_Frame,
                            this.m_TimeData,
                            population);

                        bool isTimeToWork = WorkerSystem.IsTimeToWork(
                            citizen,
                            nativeArray3[i],
                            ref this.m_EconomyParameters,
                            this.m_TimeOfDay);

                        int remainingOffDays =GetRemainingOffDays(
                            citizen,
                            ref this.m_EconomyParameters,
                            this.m_Frame,
                            this.m_TimeData,
                            population);

                        FixedString64Bytes debugMessage = default;

                        debugMessage.Append(isOffDay ? 1 : 0);
                        debugMessage.Append('-');
                        debugMessage.Append(isTimeToWork ? 1 : 0);
                        debugMessage.Append('-');
                        debugMessage.Append(remainingOffDays);

                        this.m_CustomEventData.AddParameter(debugMessage);
                        this.m_CustomEventData.Send(
                            entity,
                            CustomEventType.DebugMessage);

                        

                        
                    }
                    if (!WorkerSystem.IsTodayOffDay(citizen, ref this.m_EconomyParameters, this.m_Frame, this.m_TimeData, population) && WorkerSystem.IsTimeToWork(citizen, nativeArray3[i], ref this.m_EconomyParameters, this.m_TimeOfDay))
					{
						DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor[i];
						if (!this.m_Attendings.HasComponent(entity) && (citizen.m_State & CitizenFlags.MovingAwayReachOC) == CitizenFlags.None)
						{
							//Entity workplace = nativeArray3[i].m_Workplace;
							Entity entity2 = Entity.Null;
							if (this.m_Properties.HasComponent(workplace))
							{
								entity2 = this.m_Properties[workplace].m_Property;
							}
							else if (this.m_Buildings.HasComponent(workplace))
							{
								entity2 = workplace;
							}
							else if (this.m_OutsideConnections.HasComponent(workplace))
							{
								entity2 = workplace;
							}
							if (entity2 != Entity.Null)
							{
								if (nativeArray4[i].m_CurrentBuilding != entity2)
								{
									if (!this.m_CarKeepers.IsComponentEnabled(entity))
									{
										this.m_CarReserverQueue.Enqueue(entity);
									}
									dynamicBuffer.Add(new TripNeeded
									{
										m_TargetAgent = workplace,
										m_Purpose = Purpose.GoingToWork,
										m_Priority = 128
									});
								}
							}
							else
							{
								citizen.SetFailedEducationCount(0);
								nativeArray2[i] = citizen;
								if (this.m_Purposes.HasComponent(entity) && (this.m_Purposes[entity].m_Purpose == Purpose.GoingToWork || this.m_Purposes[entity].m_Purpose == Purpose.Working))
								{
									this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
								}
								this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity);
								this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed, Entity.Null, entity, workplace, 0f));
							}
						}
					}
				}
			}

			// Token: 0x060068D0 RID: 26832 RVA: 0x00392B5B File Offset: 0x00390D5B
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009932 RID: 39218
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x04009933 RID: 39219
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x04009934 RID: 39220
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x04009935 RID: 39221
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x04009936 RID: 39222
			public BufferTypeHandle<TripNeeded> m_TripType;

			// Token: 0x04009937 RID: 39223
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x04009938 RID: 39224
			[ReadOnly]
			public ComponentLookup<TravelPurpose> m_Purposes;

			// Token: 0x04009939 RID: 39225
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_Properties;

			// Token: 0x0400993A RID: 39226
			[ReadOnly]
			public ComponentLookup<Building> m_Buildings;

			// Token: 0x0400993B RID: 39227
			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x0400993C RID: 39228
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x0400993D RID: 39229
			[ReadOnly]
			public ComponentLookup<AttendingMeeting> m_Attendings;

			// Token: 0x0400993E RID: 39230
			[ReadOnly]
			public ComponentLookup<Population> m_PopulationData;

			// Token: 0x0400993F RID: 39231
			public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

			// Token: 0x04009940 RID: 39232
			public uint m_Frame;

			// Token: 0x04009941 RID: 39233
			public TimeData m_TimeData;

			// Token: 0x04009942 RID: 39234
			public uint m_UpdateFrameIndex;

			// Token: 0x04009943 RID: 39235
			public float m_TimeOfDay;

			// Token: 0x04009944 RID: 39236
			public Entity m_PopulationEntity;

			// Token: 0x04009945 RID: 39237
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x04009946 RID: 39238
			public NativeQueue<Entity>.ParallelWriter m_CarReserverQueue;

			// Token: 0x04009947 RID: 39239
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
		}

		// Token: 0x02001558 RID: 5464
		[BurstCompile]
		private struct WorkJob : IJobChunk
		{
			// Token: 0x060068D1 RID: 26833 RVA: 0x00392B68 File Offset: 0x00390D68
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Worker> nativeArray2 = chunk.GetNativeArray<Worker>(ref this.m_WorkerType);
				NativeArray<TravelPurpose> nativeArray3 = chunk.GetNativeArray<TravelPurpose>(ref this.m_PurposeType);
				NativeArray<Citizen> nativeArray4 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Entity entity = nativeArray[i];
					Entity workplace = nativeArray2[i].m_Workplace;
					Worker worker = nativeArray2[i];
					Citizen citizen = nativeArray4[i];
					if (chunk.Has<HealthProblem>(ref this.m_HealthProblemType))
					{
						if (nativeArray3[i].m_Purpose == Purpose.Working)
						{
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
						}
					}
					else if (!this.m_Workplaces.HasComponent(workplace))
					{
						citizen.SetFailedEducationCount(0);
						nativeArray4[i] = citizen;
						TravelPurpose travelPurpose = nativeArray3[i];
						if (travelPurpose.m_Purpose == Purpose.GoingToWork || travelPurpose.m_Purpose == Purpose.Working)
						{
							this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
						}
						this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity);
						this.m_TriggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenBecameUnemployed, Entity.Null, entity, workplace, 0f));
					}
					else if ((!WorkerSystem.IsTimeToWork(citizen, worker, ref this.m_EconomyParameters, this.m_TimeOfDay) || this.m_Attendings.HasComponent(entity)) && nativeArray3[i].m_Purpose == Purpose.Working)
					{
						this.m_CommandBuffer.RemoveComponent<TravelPurpose>(unfilteredChunkIndex, entity);
					}
				}
			}

			// Token: 0x060068D2 RID: 26834 RVA: 0x00392D09 File Offset: 0x00390F09
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009948 RID: 39240
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x04009949 RID: 39241
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x0400994A RID: 39242
			[ReadOnly]
			public ComponentTypeHandle<TravelPurpose> m_PurposeType;

			// Token: 0x0400994B RID: 39243
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x0400994C RID: 39244
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x0400994D RID: 39245
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x0400994E RID: 39246
			[ReadOnly]
			public ComponentLookup<WorkProvider> m_Workplaces;

			// Token: 0x0400994F RID: 39247
			[ReadOnly]
			public ComponentLookup<AttendingMeeting> m_Attendings;

			// Token: 0x04009950 RID: 39248
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x04009951 RID: 39249
			public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

			// Token: 0x04009952 RID: 39250
			public float m_TimeOfDay;

			// Token: 0x04009953 RID: 39251
			public uint m_UpdateFrameIndex;

			// Token: 0x04009954 RID: 39252
			public uint m_Frame;

			// Token: 0x04009955 RID: 39253
			public TimeData m_TimeData;

			// Token: 0x04009956 RID: 39254
			public int m_Population;

			// Token: 0x04009957 RID: 39255
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
		}

		// Token: 0x02001559 RID: 5465
		private struct TypeHandle
		{
			// Token: 0x060068D3 RID: 26835 RVA: 0x00392D18 File Offset: 0x00390F18
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Game_Citizens_Worker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Worker>(true);
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>(false);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
				this.__Game_Citizens_CarKeeper_RO_ComponentLookup = state.GetComponentLookup<CarKeeper>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Citizens_TravelPurpose_RO_ComponentLookup = state.GetComponentLookup<TravelPurpose>(true);
				this.__Game_Citizens_AttendingMeeting_RO_ComponentLookup = state.GetComponentLookup<AttendingMeeting>(true);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
				this.__Game_Citizens_TravelPurpose_RO_ComponentTypeHandle = state.GetComponentTypeHandle<TravelPurpose>(true);
				this.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(true);
				this.__Game_Companies_WorkProvider_RO_ComponentLookup = state.GetComponentLookup<WorkProvider>(true);
			}

			// Token: 0x04009958 RID: 39256
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009959 RID: 39257
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x0400995A RID: 39258
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x0400995B RID: 39259
			[ReadOnly]
			public ComponentTypeHandle<Worker> __Game_Citizens_Worker_RO_ComponentTypeHandle;

			// Token: 0x0400995C RID: 39260
			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			// Token: 0x0400995D RID: 39261
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x0400995E RID: 39262
			[ReadOnly]
			public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

			// Token: 0x0400995F RID: 39263
			[ReadOnly]
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentLookup;

			// Token: 0x04009960 RID: 39264
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x04009961 RID: 39265
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x04009962 RID: 39266
			[ReadOnly]
			public ComponentLookup<TravelPurpose> __Game_Citizens_TravelPurpose_RO_ComponentLookup;

			// Token: 0x04009963 RID: 39267
			[ReadOnly]
			public ComponentLookup<AttendingMeeting> __Game_Citizens_AttendingMeeting_RO_ComponentLookup;

			// Token: 0x04009964 RID: 39268
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;

			// Token: 0x04009965 RID: 39269
			[ReadOnly]
			public ComponentTypeHandle<TravelPurpose> __Game_Citizens_TravelPurpose_RO_ComponentTypeHandle;

			// Token: 0x04009966 RID: 39270
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentTypeHandle;

			// Token: 0x04009967 RID: 39271
			[ReadOnly]
			public ComponentLookup<WorkProvider> __Game_Companies_WorkProvider_RO_ComponentLookup;
		}
	}
}
