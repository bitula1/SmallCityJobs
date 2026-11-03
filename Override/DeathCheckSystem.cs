using System;
using System.Runtime.CompilerServices;
using Colossal;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game.Achievements;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Creatures;
using Game.Debug;
using Game.Notifications;
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
using UnityEngine.Scripting;
using Game;
using Game.Simulation;
using BitulaMod.SmallCity;

namespace BitulaMod
{
	// Token: 0x02001544 RID: 5444
	public partial class DeathCheckSystem : GameSystemBase, IDefaultSerializable, ISerializable
	{
        public SCInfo<SCMilestoneInfo> m_MilestoneInfo;
        // Token: 0x0600692F RID: 26927 RVA: 0x00388E7B File Offset: 0x0038707B
        public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 262144 / (DeathCheckSystem.kUpdatesPerDay * 16);
		}

		// Token: 0x06006930 RID: 26928 RVA: 0x00388E8C File Offset: 0x0038708C
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_IconCommandSystem = base.World.GetOrCreateSystemManaged<IconCommandSystem>();
			this.m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
			this.m_CityStatisticsSystem = base.World.GetOrCreateSystemManaged<CityStatisticsSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_TriggerSystem = base.World.GetOrCreateSystemManaged<TriggerSystem>();
			this.m_AchievementTriggerSystem = base.World.GetOrCreateSystemManaged<AchievementTriggerSystem>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_DeathCheckQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Citizen>(),
				ComponentType.ReadOnly<UpdateFrame>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_HealthcareSettingsQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<HealthcareParameterData>() });
			this.m_TimeSettingsQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TimeSettingsData>() });
			this.m_TimeDataQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TimeData>() });
			this.m_OldAgeDeaths = new NativeValue<int>(Allocator.Persistent);
			this.m_OldAgeDeathCounter = new NativeCounter(Allocator.Persistent);
			this.m_MilestoneInfo = SCInfo<SCMilestoneInfo>.Create(ref base.CheckedStateRef);

			base.RequireForUpdate(this.m_DeathCheckQuery);
			base.RequireForUpdate(this.m_HealthcareSettingsQuery);
		}

		// Token: 0x06006931 RID: 26929 RVA: 0x00388FF3 File Offset: 0x003871F3
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_OldAgeDeaths.Dispose();
			this.m_OldAgeDeathCounter.Dispose();
			base.OnDestroy();
		}

		// Token: 0x06006932 RID: 26930 RVA: 0x00389014 File Offset: 0x00387214
		[Preserve]
		protected override void OnUpdate()
		{
			m_MilestoneInfo.Update(ref base.CheckedStateRef);
			if (!m_MilestoneInfo.Get().IsDeathCareEnabled())
			{
				return;
			}
			uint updateFrame = SimulationUtils.GetUpdateFrame(this.m_SimulationSystem.frameIndex, DeathCheckSystem.kUpdatesPerDay, 16);
			this.m_WriteDeps.Complete();
			DeathCheckSystem.DeathCheckJob deathCheckJob = default(DeathCheckSystem.DeathCheckJob);
			deathCheckJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_LeisureType = InternalCompilerInterface.GetComponentTypeHandle<Leisure>(ref this.__TypeHandle.__Game_Citizens_Leisure_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_ResourceBuyerType = InternalCompilerInterface.GetComponentTypeHandle<ResourceBuyer>(ref this.__TypeHandle.__Game_Companies_ResourceBuyer_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_StudentType = InternalCompilerInterface.GetComponentTypeHandle<Game.Citizens.Student>(ref this.__TypeHandle.__Game_Citizens_Student_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			deathCheckJob.m_CurrentBuildings = InternalCompilerInterface.GetComponentLookup<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef);
			deathCheckJob.m_HospitalData = InternalCompilerInterface.GetComponentLookup<Game.Buildings.Hospital>(ref this.__TypeHandle.__Game_Buildings_Hospital_RO_ComponentLookup, ref base.CheckedStateRef);
			deathCheckJob.m_BuildingData = InternalCompilerInterface.GetComponentLookup<Building>(ref this.__TypeHandle.__Game_Buildings_Building_RO_ComponentLookup, ref base.CheckedStateRef);
			deathCheckJob.m_CurrentTransport = InternalCompilerInterface.GetComponentLookup<CurrentTransport>(ref this.__TypeHandle.__Game_Citizens_CurrentTransport_RO_ComponentLookup, ref base.CheckedStateRef);
			deathCheckJob.m_ResidentData = InternalCompilerInterface.GetComponentLookup<Game.Creatures.Resident>(ref this.__TypeHandle.__Game_Creatures_Resident_RO_ComponentLookup, ref base.CheckedStateRef);
			deathCheckJob.m_CityModifiers = InternalCompilerInterface.GetBufferLookup<CityModifier>(ref this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref base.CheckedStateRef);
			deathCheckJob.m_Students = InternalCompilerInterface.GetBufferLookup<Game.Buildings.Student>(ref this.__TypeHandle.__Game_Buildings_Student_RO_BufferLookup, ref base.CheckedStateRef);
			deathCheckJob.m_HouseholdCitizens = InternalCompilerInterface.GetBufferLookup<HouseholdCitizen>(ref this.__TypeHandle.__Game_Citizens_HouseholdCitizen_RO_BufferLookup, ref base.CheckedStateRef);
			deathCheckJob.m_UpdateFrameIndex = updateFrame;
			deathCheckJob.m_RandomSeed = RandomSeed.Next();
			deathCheckJob.m_HealthcareParameterData = this.m_HealthcareSettingsQuery.GetSingleton<HealthcareParameterData>();
			deathCheckJob.m_City = this.m_CitySystem.City;
			deathCheckJob.m_TriggerBuffer = this.m_TriggerSystem.CreateActionBuffer().AsParallelWriter();
			JobHandle jobHandle;
			deathCheckJob.m_StatisticsEventQueue = this.m_CityStatisticsSystem.GetStatisticsEventQueue(out jobHandle).AsParallelWriter();
			deathCheckJob.m_IconCommandBuffer = this.m_IconCommandSystem.CreateCommandBuffer();
			deathCheckJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			deathCheckJob.m_PatientsRecoveredCounter = this.m_AchievementTriggerSystem.m_PatientsTreatedCounter.ToConcurrent();
			deathCheckJob.m_OldAgeDeathCounter = this.m_OldAgeDeathCounter.ToConcurrent();
			deathCheckJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			deathCheckJob.m_TimeSettings = this.m_TimeSettingsQuery.GetSingleton<TimeSettingsData>();
			deathCheckJob.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			deathCheckJob.m_NormalizedTime = this.m_TimeSystem.normalizedTime;
			deathCheckJob.m_NewDeathRate = this.m_UseNewCurve;
			DeathCheckSystem.DeathCheckJob deathCheckJob2 = deathCheckJob;
			base.Dependency = deathCheckJob2.ScheduleParallel(this.m_DeathCheckQuery, JobHandle.CombineDependencies(base.Dependency, jobHandle));
			DeathCheckSystem.SumDeathJob sumDeathJob = default(DeathCheckSystem.SumDeathJob);
			sumDeathJob.m_DebugDeaths = this.m_OldAgeDeaths;
			sumDeathJob.m_DebugDeathCount = this.m_OldAgeDeathCounter;
			DeathCheckSystem.SumDeathJob sumDeathJob2 = sumDeathJob;
			base.Dependency = sumDeathJob2.Schedule(base.Dependency);
			this.m_WriteDeps = JobHandle.CombineDependencies(this.m_WriteDeps, base.Dependency);
			this.m_IconCommandSystem.AddCommandBufferWriter(base.Dependency);
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
			this.m_CityStatisticsSystem.AddWriter(base.Dependency);
			this.m_TriggerSystem.AddActionBufferWriter(base.Dependency);
		}

		// Token: 0x06006933 RID: 26931 RVA: 0x00389428 File Offset: 0x00387628
		public static void PerformAfterDeathActions(Entity citizen, Entity household, NativeQueue<TriggerAction>.ParallelWriter triggerBuffer, NativeQueue<StatisticsEvent>.ParallelWriter statisticsEventQueue, ref BufferLookup<HouseholdCitizen> householdCitizens)
		{
			triggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizenDied, Entity.Null, citizen, Entity.Null, 0f));
			DynamicBuffer<HouseholdCitizen> dynamicBuffer;
			if (household != Entity.Null && householdCitizens.TryGetBuffer(household, out dynamicBuffer))
			{
				for (int i = 0; i < dynamicBuffer.Length; i++)
				{
					if (dynamicBuffer[i].m_Citizen != citizen)
					{
						triggerBuffer.Enqueue(new TriggerAction(TriggerType.CitizensFamilyMemberDied, Entity.Null, dynamicBuffer[i].m_Citizen, citizen, 0f));
					}
				}
			}
			statisticsEventQueue.Enqueue(new StatisticsEvent
			{
				m_Statistic = StatisticType.DeathRate,
				m_Change = 1f
			});
		}

		// Token: 0x06006934 RID: 26932 RVA: 0x003894DE File Offset: 0x003876DE
		public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
		{
			writer.Write(this.m_UseNewCurve);
		}

		// Token: 0x06006935 RID: 26933 RVA: 0x003894F4 File Offset: 0x003876F4
		public void Deserialize<TReader>(TReader reader) where TReader : IReader
		{
			if (reader.context.format.Has<FormatTags>(FormatTags.EasyModeDeathRateFix))
			{
				reader.Read(out this.m_UseNewCurve);
				return;
			}
			this.m_UseNewCurve = false;
		}

		// Token: 0x06006936 RID: 26934 RVA: 0x0038953D File Offset: 0x0038773D
		public void SetDefaults(Context context)
		{
			this.m_UseNewCurve = context.purpose == global::Colossal.Serialization.Entities.Purpose.NewGame;
		}

		// Token: 0x06006937 RID: 26935 RVA: 0x00389550 File Offset: 0x00387750
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x06006938 RID: 26936 RVA: 0x00389571 File Offset: 0x00387771
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x06006939 RID: 26937 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public DeathCheckSystem()
		{
		}

		// Token: 0x040095C6 RID: 38342
		public static readonly int kUpdatesPerDay = 16;

		// Token: 0x040095C7 RID: 38343
		public static readonly int kMaxAgeInGameYear = 9;

		// Token: 0x040095C8 RID: 38344
		private SimulationSystem m_SimulationSystem;

		// Token: 0x040095C9 RID: 38345
		private IconCommandSystem m_IconCommandSystem;

		// Token: 0x040095CA RID: 38346
		private CitySystem m_CitySystem;

		// Token: 0x040095CB RID: 38347
		private CityStatisticsSystem m_CityStatisticsSystem;

		// Token: 0x040095CC RID: 38348
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x040095CD RID: 38349
		private TimeSystem m_TimeSystem;

		// Token: 0x040095CE RID: 38350
		private EntityQuery m_DeathCheckQuery;

		// Token: 0x040095CF RID: 38351
		private EntityQuery m_HealthcareSettingsQuery;

		// Token: 0x040095D0 RID: 38352
		private EntityQuery m_TimeSettingsQuery;

		// Token: 0x040095D1 RID: 38353
		private EntityQuery m_TimeDataQuery;

		// Token: 0x040095D2 RID: 38354
		private TriggerSystem m_TriggerSystem;

		// Token: 0x040095D3 RID: 38355
		private AchievementTriggerSystem m_AchievementTriggerSystem;

		// Token: 0x040095D4 RID: 38356
		[DebugWatchValue]
		private NativeValue<int> m_OldAgeDeaths;

		// Token: 0x040095D5 RID: 38357
		private NativeCounter m_OldAgeDeathCounter;

		// Token: 0x040095D6 RID: 38358
		[DebugWatchDeps]
		private JobHandle m_WriteDeps;

		// Token: 0x040095D7 RID: 38359
		private bool m_UseNewCurve;

		// Token: 0x040095D8 RID: 38360
		private DeathCheckSystem.TypeHandle __TypeHandle;

		// Token: 0x02001545 RID: 5445
		[BurstCompile]
		private struct DeathCheckJob : IJobChunk
		{
			// Token: 0x0600693B RID: 26939 RVA: 0x003895A8 File Offset: 0x003877A8
			private void Die(ArchetypeChunk chunk, int chunkIndex, int i, Entity citizen, Entity household, NativeArray<Game.Citizens.Student> students, NativeArray<HealthProblem> healthProblems)
			{
				if (!healthProblems.IsCreated)
				{
					HealthProblem healthProblem = new HealthProblem
					{
						m_Flags = (HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport)
					};
					this.m_CommandBuffer.AddComponent<HealthProblem>(chunkIndex, citizen, healthProblem);
				}
				else
				{
					HealthProblem healthProblem2 = healthProblems[i];
					if ((healthProblem2.m_Flags & HealthProblemFlags.RequireTransport) != HealthProblemFlags.None)
					{
						this.m_IconCommandBuffer.Remove(citizen, this.m_HealthcareParameterData.m_AmbulanceNotificationPrefab, default(Entity), (IconFlags)0);
						healthProblem2.m_Timer = 0;
					}
					healthProblem2.m_Flags &= ~(HealthProblemFlags.Sick | HealthProblemFlags.Injured);
					healthProblem2.m_Flags |= HealthProblemFlags.Dead | HealthProblemFlags.RequireTransport;
					healthProblems[i] = healthProblem2;
				}
				DeathCheckSystem.PerformAfterDeathActions(citizen, household, this.m_TriggerBuffer, this.m_StatisticsEventQueue, ref this.m_HouseholdCitizens);
				if (chunk.Has<Game.Citizens.Student>(ref this.m_StudentType))
				{
					Entity school = students[i].m_School;
					if (this.m_Students.HasBuffer(school))
					{
						this.m_CommandBuffer.AddComponent<StudentsRemoved>(chunkIndex, school);
					}
					this.m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(chunkIndex, citizen);
				}
				if (chunk.Has<Worker>(ref this.m_WorkerType))
				{
					this.m_CommandBuffer.RemoveComponent<Worker>(chunkIndex, citizen);
				}
				if (chunk.Has<ResourceBuyer>(ref this.m_ResourceBuyerType))
				{
					this.m_CommandBuffer.RemoveComponent<ResourceBuyer>(chunkIndex, citizen);
				}
				if (chunk.Has<Leisure>(ref this.m_LeisureType))
				{
					this.m_CommandBuffer.RemoveComponent<Leisure>(chunkIndex, citizen);
				}
			}

			// Token: 0x0600693C RID: 26940 RVA: 0x00389700 File Offset: 0x00387900
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				DynamicBuffer<CityModifier> dynamicBuffer = this.m_CityModifiers[this.m_City];
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<HealthProblem> nativeArray2 = chunk.GetNativeArray<HealthProblem>(ref this.m_HealthProblemType);
				NativeArray<Citizen> nativeArray3 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<Game.Citizens.Student> nativeArray4 = chunk.GetNativeArray<Game.Citizens.Student>(ref this.m_StudentType);
				NativeArray<HouseholdMember> nativeArray5 = chunk.GetNativeArray<HouseholdMember>(ref this.m_HouseholdMemberType);
				int i = 0;
				while (i < chunk.Count)
				{
					Entity entity = nativeArray[i];
					Entity entity2 = ((nativeArray5.Length != 0) ? nativeArray5[i].m_Household : Entity.Null);
					Citizen citizen = nativeArray3[i];
					if (!this.m_CurrentTransport.HasComponent(entity))
					{
						goto IL_010C;
					}
					CurrentTransport currentTransport = this.m_CurrentTransport[entity];
					if (!this.m_ResidentData.HasComponent(currentTransport.m_CurrentTransport) || (this.m_ResidentData[currentTransport.m_CurrentTransport].m_Flags & ResidentFlags.InVehicle) == ResidentFlags.None)
					{
						goto IL_010C;
					}
					IL_0364:
					i++;
					continue;
					IL_010C:
					if (nativeArray2.IsCreated && (nativeArray2[i].m_Flags & HealthProblemFlags.Dead) != HealthProblemFlags.None)
					{
						goto IL_0364;
					}
					float num = citizen.GetPseudoRandom(CitizenPseudoRandom.Death).NextFloat();
					float num2 = this.m_NormalizedTime - 0.5f;
					float num3 = citizen.GetAgeInDays(this.m_SimulationFrame, this.m_TimeData) + num2;
					float num4 = 1f * num3 / (float)this.m_TimeSettings.m_DaysPerYear / (float)DeathCheckSystem.kMaxAgeInGameYear;
					if (num < (this.m_NewDeathRate ? this.m_HealthcareParameterData.m_DeathRate : this.m_HealthcareParameterData.m_LegacyDeathRate).Evaluate(num4))
					{
						this.m_OldAgeDeathCounter.Increment();
						this.Die(chunk, unfilteredChunkIndex, i, entity, entity2, nativeArray4, nativeArray2);
						goto IL_0364;
					}
					if (!nativeArray2.IsCreated || (nativeArray2[i].m_Flags & (HealthProblemFlags.Sick | HealthProblemFlags.Injured)) == HealthProblemFlags.None)
					{
						goto IL_0364;
					}
					HealthProblem healthProblem = nativeArray2[i];
					int num5 = (int)(10 - citizen.m_Health / 10);
					int num6 = num5 * num5;
					num6 += 8;
					if (random.NextInt(DeathCheckSystem.kUpdatesPerDay * 1000) <= num6)
					{
						this.Die(chunk, unfilteredChunkIndex, i, entity, entity2, nativeArray4, nativeArray2);
						goto IL_0364;
					}
					float num7 = MathUtils.Logistic(3f, 1000f, 6f, (float)num5 / 10f - 0.35f);
					int num8 = 0;
					if (this.m_CurrentBuildings.HasComponent(entity))
					{
						Entity currentBuilding = this.m_CurrentBuildings[entity].m_CurrentBuilding;
						if (this.m_BuildingData.HasComponent(currentBuilding) && !BuildingUtils.CheckOption(this.m_BuildingData[currentBuilding], BuildingOption.Inactive) && this.m_HospitalData.HasComponent(currentBuilding))
						{
							num8 = (int)this.m_HospitalData[currentBuilding].m_TreatmentBonus;
						}
					}
					num7 -= (float)(10 * num8);
					CityUtils.ApplyModifier(ref num7, dynamicBuffer, CityModifierType.RecoveryFailChange);
					if (random.NextFloat(1000f) >= num7)
					{
						if ((healthProblem.m_Flags & HealthProblemFlags.RequireTransport) != HealthProblemFlags.None)
						{
							this.m_IconCommandBuffer.Remove(entity, this.m_HealthcareParameterData.m_AmbulanceNotificationPrefab, default(Entity), (IconFlags)0);
							healthProblem.m_Timer = 0;
						}
						healthProblem.m_Flags &= ~(HealthProblemFlags.Sick | HealthProblemFlags.Injured | HealthProblemFlags.RequireTransport);
						nativeArray2[i] = healthProblem;
						this.m_PatientsRecoveredCounter.Increment();
						goto IL_0364;
					}
					goto IL_0364;
				}
			}

			// Token: 0x0600693D RID: 26941 RVA: 0x00389A84 File Offset: 0x00387C84
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040095D9 RID: 38361
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x040095DA RID: 38362
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040095DB RID: 38363
			[ReadOnly]
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x040095DC RID: 38364
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x040095DD RID: 38365
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			// Token: 0x040095DE RID: 38366
			[ReadOnly]
			public ComponentTypeHandle<Game.Citizens.Student> m_StudentType;

			// Token: 0x040095DF RID: 38367
			[ReadOnly]
			public ComponentTypeHandle<ResourceBuyer> m_ResourceBuyerType;

			// Token: 0x040095E0 RID: 38368
			[ReadOnly]
			public ComponentTypeHandle<Leisure> m_LeisureType;

			// Token: 0x040095E1 RID: 38369
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x040095E2 RID: 38370
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildings;

			// Token: 0x040095E3 RID: 38371
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Hospital> m_HospitalData;

			// Token: 0x040095E4 RID: 38372
			[ReadOnly]
			public ComponentLookup<Building> m_BuildingData;

			// Token: 0x040095E5 RID: 38373
			[ReadOnly]
			public ComponentLookup<Game.Creatures.Resident> m_ResidentData;

			// Token: 0x040095E6 RID: 38374
			[ReadOnly]
			public ComponentLookup<CurrentTransport> m_CurrentTransport;

			// Token: 0x040095E7 RID: 38375
			[ReadOnly]
			public BufferLookup<CityModifier> m_CityModifiers;

			// Token: 0x040095E8 RID: 38376
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> m_Students;

			// Token: 0x040095E9 RID: 38377
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

			// Token: 0x040095EA RID: 38378
			[ReadOnly]
			public uint m_UpdateFrameIndex;

			// Token: 0x040095EB RID: 38379
			[ReadOnly]
			public RandomSeed m_RandomSeed;

			// Token: 0x040095EC RID: 38380
			[ReadOnly]
			public HealthcareParameterData m_HealthcareParameterData;

			// Token: 0x040095ED RID: 38381
			[ReadOnly]
			public Entity m_City;

			// Token: 0x040095EE RID: 38382
			public NativeQueue<StatisticsEvent>.ParallelWriter m_StatisticsEventQueue;

			// Token: 0x040095EF RID: 38383
			public NativeQueue<TriggerAction>.ParallelWriter m_TriggerBuffer;

			// Token: 0x040095F0 RID: 38384
			public IconCommandBuffer m_IconCommandBuffer;

			// Token: 0x040095F1 RID: 38385
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040095F2 RID: 38386
			public NativeCounter.Concurrent m_PatientsRecoveredCounter;

			// Token: 0x040095F3 RID: 38387
			public NativeCounter.Concurrent m_OldAgeDeathCounter;

			// Token: 0x040095F4 RID: 38388
			public TimeSettingsData m_TimeSettings;

			// Token: 0x040095F5 RID: 38389
			public TimeData m_TimeData;

			// Token: 0x040095F6 RID: 38390
			public uint m_SimulationFrame;

			// Token: 0x040095F7 RID: 38391
			public float m_NormalizedTime;

			// Token: 0x040095F8 RID: 38392
			public bool m_NewDeathRate;
		}

		// Token: 0x02001546 RID: 5446
		[BurstCompile]
		private struct SumDeathJob : IJob
		{
			// Token: 0x0600693E RID: 26942 RVA: 0x00389A91 File Offset: 0x00387C91
			public void Execute()
			{
				this.m_DebugDeaths.value = this.m_DebugDeathCount.Count;
			}

			// Token: 0x040095F9 RID: 38393
			public NativeCounter m_DebugDeathCount;

			// Token: 0x040095FA RID: 38394
			public NativeValue<int> m_DebugDeaths;
		}

		// Token: 0x02001547 RID: 5447
		private struct TypeHandle
		{
			// Token: 0x0600693F RID: 26943 RVA: 0x00389AAC File Offset: 0x00387CAC
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Citizen_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(true);
				this.__Game_Citizens_Worker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Worker>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(true);
				this.__Game_Citizens_HealthProblem_RW_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(false);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_Leisure_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Leisure>(true);
				this.__Game_Companies_ResourceBuyer_RO_ComponentTypeHandle = state.GetComponentTypeHandle<ResourceBuyer>(true);
				this.__Game_Citizens_Student_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Citizens.Student>(true);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentLookup = state.GetComponentLookup<CurrentBuilding>(true);
				this.__Game_Buildings_Hospital_RO_ComponentLookup = state.GetComponentLookup<Game.Buildings.Hospital>(true);
				this.__Game_Buildings_Building_RO_ComponentLookup = state.GetComponentLookup<Building>(true);
				this.__Game_Citizens_CurrentTransport_RO_ComponentLookup = state.GetComponentLookup<CurrentTransport>(true);
				this.__Game_Creatures_Resident_RO_ComponentLookup = state.GetComponentLookup<Game.Creatures.Resident>(true);
				this.__Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(true);
				this.__Game_Buildings_Student_RO_BufferLookup = state.GetBufferLookup<Game.Buildings.Student>(true);
				this.__Game_Citizens_HouseholdCitizen_RO_BufferLookup = state.GetBufferLookup<HouseholdCitizen>(true);
			}

			// Token: 0x040095FB RID: 38395
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x040095FC RID: 38396
			[ReadOnly]
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RO_ComponentTypeHandle;

			// Token: 0x040095FD RID: 38397
			[ReadOnly]
			public ComponentTypeHandle<Worker> __Game_Citizens_Worker_RO_ComponentTypeHandle;

			// Token: 0x040095FE RID: 38398
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			// Token: 0x040095FF RID: 38399
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RW_ComponentTypeHandle;

			// Token: 0x04009600 RID: 38400
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x04009601 RID: 38401
			[ReadOnly]
			public ComponentTypeHandle<Leisure> __Game_Citizens_Leisure_RO_ComponentTypeHandle;

			// Token: 0x04009602 RID: 38402
			[ReadOnly]
			public ComponentTypeHandle<ResourceBuyer> __Game_Companies_ResourceBuyer_RO_ComponentTypeHandle;

			// Token: 0x04009603 RID: 38403
			[ReadOnly]
			public ComponentTypeHandle<Game.Citizens.Student> __Game_Citizens_Student_RO_ComponentTypeHandle;

			// Token: 0x04009604 RID: 38404
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			// Token: 0x04009605 RID: 38405
			[ReadOnly]
			public ComponentLookup<Game.Buildings.Hospital> __Game_Buildings_Hospital_RO_ComponentLookup;

			// Token: 0x04009606 RID: 38406
			[ReadOnly]
			public ComponentLookup<Building> __Game_Buildings_Building_RO_ComponentLookup;

			// Token: 0x04009607 RID: 38407
			[ReadOnly]
			public ComponentLookup<CurrentTransport> __Game_Citizens_CurrentTransport_RO_ComponentLookup;

			// Token: 0x04009608 RID: 38408
			[ReadOnly]
			public ComponentLookup<Game.Creatures.Resident> __Game_Creatures_Resident_RO_ComponentLookup;

			// Token: 0x04009609 RID: 38409
			[ReadOnly]
			public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;

			// Token: 0x0400960A RID: 38410
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> __Game_Buildings_Student_RO_BufferLookup;

			// Token: 0x0400960B RID: 38411
			[ReadOnly]
			public BufferLookup<HouseholdCitizen> __Game_Citizens_HouseholdCitizen_RO_BufferLookup;
		}
	}
}
