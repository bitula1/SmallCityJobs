using System;
using System.Runtime.CompilerServices;
using Game.Agents;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Economy;
using Game.Prefabs;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Internal;
using Unity.Mathematics;
using UnityEngine.Scripting;
using Game;
using Game.Simulation;
using Game.Net;

namespace BitulaMod
{
	// Token: 0x020015C2 RID: 5570
	public partial class ApplyToSchoolSystem : GameSystemBase
	{
		// Token: 0x06006A59 RID: 27225 RVA: 0x002F0308 File Offset: 0x002EE508
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 512;
		}

		// Token: 0x06006A5A RID: 27226 RVA: 0x003AEF0C File Offset: 0x003AD10C
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_CitySystem = base.World.GetOrCreateSystemManaged<CitySystem>();
			this.m_CitizenGroup = base.GetEntityQuery(new EntityQueryDesc[]
			{
				new EntityQueryDesc
				{
					All = new ComponentType[]
					{
						ComponentType.ReadWrite<Citizen>(),
						ComponentType.ReadOnly<UpdateFrame>()
					},
					None = new ComponentType[]
					{
						ComponentType.ReadOnly<HealthProblem>(),
						ComponentType.ReadOnly<HasJobSeeker>(),
						ComponentType.ReadOnly<HasSchoolSeeker>(),
						ComponentType.ReadOnly<Game.Citizens.Student>(),
						ComponentType.ReadOnly<Deleted>()
					}
				}
			});
			base.RequireForUpdate(this.m_CitizenGroup);
			base.RequireForUpdate<EconomyParameterData>();
			base.RequireForUpdate<TimeData>();
		}

		// Token: 0x06006A5B RID: 27227 RVA: 0x003AEFF4 File Offset: 0x003AD1F4
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			ApplyToSchoolSystem.ApplyToSchoolJob applyToSchoolJob = default(ApplyToSchoolSystem.ApplyToSchoolJob);
			applyToSchoolJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			applyToSchoolJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			applyToSchoolJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			applyToSchoolJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			applyToSchoolJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_CityModifiers = InternalCompilerInterface.GetBufferLookup<CityModifier>(ref this.__TypeHandle.__Game_City_CityModifier_RO_BufferLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_SchoolDatas = InternalCompilerInterface.GetComponentLookup<SchoolData>(ref this.__TypeHandle.__Game_Prefabs_SchoolData_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_HouseholdDatas = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_Resources = InternalCompilerInterface.GetBufferLookup<Resources>(ref this.__TypeHandle.__Game_Economy_Resources_RO_BufferLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_Fees = InternalCompilerInterface.GetBufferLookup<ServiceFee>(ref this.__TypeHandle.__Game_City_ServiceFee_RO_BufferLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_MovingAways = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			applyToSchoolJob.m_SchoolSeekerCooldowns = InternalCompilerInterface.GetComponentLookup<SchoolSeekerCooldown>(ref this.__TypeHandle.__Game_Citizens_SchoolSeekerCooldown_RO_ComponentLookup, ref base.CheckedStateRef);
            applyToSchoolJob.m_Population = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
            applyToSchoolJob.m_RandomSeed = RandomSeed.Next();
			applyToSchoolJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			applyToSchoolJob.m_EconomyParameters = this.__query_2069025490_0.GetSingleton<EconomyParameterData>();
			applyToSchoolJob.m_EducationParameters = this.__query_2069025490_1.GetSingleton<EducationParameterData>();
			applyToSchoolJob.m_TimeData = this.__query_2069025490_2.GetSingleton<TimeData>();
			applyToSchoolJob.m_City = this.m_CitySystem.City;
			applyToSchoolJob.m_UpdateFrameIndex = updateFrameWithInterval;
			applyToSchoolJob.m_DebugFastApplySchool = this.debugFastApplySchool;
			applyToSchoolJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
            applyToSchoolJob.m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone;
            applyToSchoolJob.m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement;
            applyToSchoolJob.m_AdultsWork = Mod.Settings.PrioritizeAdultEmployment;
            ApplyToSchoolSystem.ApplyToSchoolJob applyToSchoolJob2 = applyToSchoolJob;
			base.Dependency = applyToSchoolJob2.ScheduleParallel(this.m_CitizenGroup, base.Dependency);
			this.m_EndFrameBarrier.AddJobHandleForProducer(base.Dependency);
		}

		// Token: 0x06006A5C RID: 27228 RVA: 0x003AF298 File Offset: 0x003AD498
		public static float GetEnteringProbability(CitizenAge age, bool worker, int level, int wellbeing, float willingness, DynamicBuffer<CityModifier> cityModifiers, ref EducationParameterData educationParameterData)
		{
			if (level == 1)
			{
				if (age != CitizenAge.Child)
				{
					return 0f;
				}
				return 1f;
			}
			else
			{
				if (age == CitizenAge.Child || age == CitizenAge.Elderly)
				{
					return 0f;
				}
				if (level == 2)
				{
					if (age != CitizenAge.Adult && !worker)
					{
						return educationParameterData.m_EnterHighSchoolProbability;
					}
					return educationParameterData.m_AdultEnterHighSchoolProbability;
				}
				else
				{
					float num = (float)wellbeing / 60f * (0.5f + willingness);
					if (level == 3)
					{
						return 0.5f * (worker ? educationParameterData.m_WorkerContinueEducationProbability : 1f) * math.log(1.6f * num + 1f);
					}
					if (level == 4)
					{
						float num2 = 0.3f * (worker ? educationParameterData.m_WorkerContinueEducationProbability : 1f) * num;
						CityUtils.ApplyModifier(ref num2, cityModifiers, CityModifierType.UniversityInterest);
						return num2;
					}
					return 0f;
				}
			}
		}

		// Token: 0x06006A5D RID: 27229 RVA: 0x003AF354 File Offset: 0x003AD554
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			this.__query_2069025490_0 = entityQueryBuilder.WithAll<EconomyParameterData>().WithOptions(EntityQueryOptions.IncludeSystems).Build(ref state);
			entityQueryBuilder.Reset();
			this.__query_2069025490_1 = entityQueryBuilder.WithAll<EducationParameterData>().WithOptions(EntityQueryOptions.IncludeSystems).Build(ref state);
			entityQueryBuilder.Reset();
			this.__query_2069025490_2 = entityQueryBuilder.WithAll<TimeData>().WithOptions(EntityQueryOptions.IncludeSystems).Build(ref state);
			entityQueryBuilder.Reset();
			entityQueryBuilder.Dispose();
		}

		// Token: 0x06006A5E RID: 27230 RVA: 0x003AF3ED File Offset: 0x003AD5ED
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x06006A5F RID: 27231 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public ApplyToSchoolSystem()
		{
		}

		// Token: 0x0400A05E RID: 41054
		public static readonly int kCoolDown = 20000;

		// Token: 0x0400A05F RID: 41055
		public const int kElementaryMinAgeInDays = 10;

		// Token: 0x0400A060 RID: 41056
		public const uint UPDATE_INTERVAL = 8192U;

		// Token: 0x0400A061 RID: 41057
		public bool debugFastApplySchool;

		// Token: 0x0400A062 RID: 41058
		private EntityQuery m_CitizenGroup;

		// Token: 0x0400A063 RID: 41059
		private SimulationSystem m_SimulationSystem;

		// Token: 0x0400A064 RID: 41060
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x0400A065 RID: 41061
		private CitySystem m_CitySystem;

		// Token: 0x0400A066 RID: 41062
		private ApplyToSchoolSystem.TypeHandle __TypeHandle;

		// Token: 0x0400A067 RID: 41063
		private EntityQuery __query_2069025490_0;

		// Token: 0x0400A068 RID: 41064
		private EntityQuery __query_2069025490_1;

		// Token: 0x0400A069 RID: 41065
		private EntityQuery __query_2069025490_2;

		// Token: 0x020015C3 RID: 5571
		[BurstCompile]
		public struct ApplyToSchoolJob : IJobChunk
		{
            public int m_JobSeekerMilestone;
            public int m_JobSeekerFailureIncrement;
			public bool m_AdultsWork;
            private bool failedSchoolApplication(int population, ref Unity.Mathematics.Random random)
            {
                int passedMilestones = math.max(0, population - 1) / m_JobSeekerMilestone;

                int failurePercentage = math.max(
                    0,
                    100 - passedMilestones * m_JobSeekerFailureIncrement);

                return random.NextInt(100) < failurePercentage;
            }
            // Token: 0x06006A61 RID: 27233 RVA: 0x003AF420 File Offset: 0x003AD620
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (!this.m_DebugFastApplySchool && chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<Worker> nativeArray3 = chunk.GetNativeArray<Worker>(ref this.m_WorkerType);
				DynamicBuffer<CityModifier> dynamicBuffer = this.m_CityModifiers[this.m_City];
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				for (int i = 0; i < chunk.Count; i++)
				{
					Citizen citizen = nativeArray2[i];
					CitizenAge age = citizen.GetAge();
					if (age != CitizenAge.Elderly && (this.m_DebugFastApplySchool || !this.m_SchoolSeekerCooldowns.HasComponent(nativeArray[i]) || (ulong)this.m_SimulationFrame >= (ulong)this.m_SchoolSeekerCooldowns[nativeArray[i]].m_SimulationFrame + (ulong)((long)ApplyToSchoolSystem.kCoolDown)))
					{
						SchoolLevel schoolLevel;
						if (age == CitizenAge.Child && !this.m_DebugFastApplySchool)
						{
							if (TimeSystem.GetDay(this.m_SimulationFrame, this.m_TimeData) - (int)citizen.m_BirthDay < 10)
							{
								goto IL_031F;
							}
							schoolLevel = SchoolLevel.Elementary;
						}
						else
						{
							schoolLevel = citizen.GetEducationLevel() + SchoolLevel.Elementary;
						}
						int failedEducationCount = citizen.GetFailedEducationCount();
						if (failedEducationCount == 0 && age > CitizenAge.Teen && schoolLevel == SchoolLevel.College)
						{
							schoolLevel = SchoolLevel.University;
						}
						bool flag = age == CitizenAge.Child || (age == CitizenAge.Teen && schoolLevel >= SchoolLevel.HighSchool && schoolLevel < SchoolLevel.University) || (age == CitizenAge.Adult && schoolLevel >= SchoolLevel.HighSchool);
						Entity household = this.m_HouseholdMembers[nativeArray[i]].m_Household;
						if (this.m_DebugFastApplySchool || (flag && CitizenUtils.HasMovedIn(household, this.m_HouseholdDatas)))
						{
							float num = citizen.GetPseudoRandom(CitizenPseudoRandom.StudyWillingness).NextFloat();
							float enteringProbability = ApplyToSchoolSystem.GetEnteringProbability(age, nativeArray3.IsCreated, (int)schoolLevel, (int)citizen.m_WellBeing, num, dynamicBuffer, ref this.m_EducationParameters);
							if ((this.m_DebugFastApplySchool || random.NextFloat(1f) < enteringProbability) 
								&& (!m_AdultsWork || age != CitizenAge.Adult || !failedSchoolApplication(m_Population[m_City].m_Population, ref random)))
							{
								if (this.m_PropertyRenters.HasComponent(household) && !this.m_TouristHouseholds.HasComponent(household) && !this.m_MovingAways.HasComponent(household))
								{
									Entity property = this.m_PropertyRenters[household].m_Property;
									Entity entity = this.m_CommandBuffer.CreateEntity(unfilteredChunkIndex);
									this.m_CommandBuffer.AddComponent<Owner>(unfilteredChunkIndex, entity, new Owner
									{
										m_Owner = nativeArray[i]
									});
									this.m_CommandBuffer.AddComponent<SchoolSeeker>(unfilteredChunkIndex, entity, new SchoolSeeker
									{
										m_Level = (int)schoolLevel
									});
									this.m_CommandBuffer.AddComponent<CurrentBuilding>(unfilteredChunkIndex, entity, new CurrentBuilding
									{
										m_CurrentBuilding = property
									});
									this.m_CommandBuffer.AddComponent<HasSchoolSeeker>(unfilteredChunkIndex, nativeArray[i], new HasSchoolSeeker
									{
										m_Seeker = entity
									});
								}
							}
							else if (schoolLevel > SchoolLevel.HighSchool)
							{
								citizen.SetFailedEducationCount(math.min(3, failedEducationCount + 1));
								nativeArray2[i] = citizen;
								this.m_CommandBuffer.AddComponent<SchoolSeekerCooldown>(unfilteredChunkIndex, nativeArray[i], new SchoolSeekerCooldown
								{
									m_SimulationFrame = this.m_SimulationFrame
								});
							}
						}
					}
					IL_031F:;
				}
			}

			// Token: 0x06006A62 RID: 27234 RVA: 0x003AF75F File Offset: 0x003AD95F
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x0400A06A RID: 41066
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x0400A06B RID: 41067
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x0400A06C RID: 41068
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x0400A06D RID: 41069
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x0400A06E RID: 41070
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x0400A06F RID: 41071
			[ReadOnly]
			public ComponentLookup<SchoolData> m_SchoolDatas;

			// Token: 0x0400A070 RID: 41072
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x0400A071 RID: 41073
			[ReadOnly]
			public ComponentLookup<Household> m_HouseholdDatas;

			// Token: 0x0400A072 RID: 41074
			[ReadOnly]
			public BufferLookup<Resources> m_Resources;

			// Token: 0x0400A073 RID: 41075
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x0400A074 RID: 41076
			[ReadOnly]
			public BufferLookup<CityModifier> m_CityModifiers;

			// Token: 0x0400A075 RID: 41077
			[ReadOnly]
			public BufferLookup<ServiceFee> m_Fees;

			// Token: 0x0400A076 RID: 41078
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x0400A077 RID: 41079
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAways;

			// Token: 0x0400A078 RID: 41080
			[ReadOnly]
			public ComponentLookup<SchoolSeekerCooldown> m_SchoolSeekerCooldowns;

			// Token: 0x0400A079 RID: 41081
			[ReadOnly]
			public RandomSeed m_RandomSeed;

			// Token: 0x0400A07A RID: 41082
			public uint m_UpdateFrameIndex;

			// Token: 0x0400A07B RID: 41083
			public Entity m_City;

			// Token: 0x0400A07C RID: 41084
			public uint m_SimulationFrame;

			// Token: 0x0400A07D RID: 41085
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x0400A07E RID: 41086
			public EducationParameterData m_EducationParameters;

			// Token: 0x0400A07F RID: 41087
			public TimeData m_TimeData;

			// Token: 0x0400A080 RID: 41088
			public bool m_DebugFastApplySchool;

			// Token: 0x0400A081 RID: 41089
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

            public ComponentLookup<Population> m_Population;
        }

		// Token: 0x020015C4 RID: 5572
		private struct TypeHandle
		{
			// Token: 0x06006A63 RID: 27235 RVA: 0x003AF76C File Offset: 0x003AD96C
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_Worker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Worker>(true);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_City_CityModifier_RO_BufferLookup = state.GetBufferLookup<CityModifier>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Prefabs_SchoolData_RO_ComponentLookup = state.GetComponentLookup<SchoolData>(true);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Economy_Resources_RO_BufferLookup = state.GetBufferLookup<Resources>(true);
				this.__Game_City_ServiceFee_RO_BufferLookup = state.GetBufferLookup<ServiceFee>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Agents_MovingAway_RO_ComponentLookup = state.GetComponentLookup<MovingAway>(true);
				this.__Game_Citizens_SchoolSeekerCooldown_RO_ComponentLookup = state.GetComponentLookup<SchoolSeekerCooldown>(true);
                this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(isReadOnly: true);
            }

			// Token: 0x0400A082 RID: 41090
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x0400A083 RID: 41091
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x0400A084 RID: 41092
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x0400A085 RID: 41093
			[ReadOnly]
			public ComponentTypeHandle<Worker> __Game_Citizens_Worker_RO_ComponentTypeHandle;

			// Token: 0x0400A086 RID: 41094
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x0400A087 RID: 41095
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x0400A088 RID: 41096
			[ReadOnly]
			public BufferLookup<CityModifier> __Game_City_CityModifier_RO_BufferLookup;

			// Token: 0x0400A089 RID: 41097
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x0400A08A RID: 41098
			[ReadOnly]
			public ComponentLookup<SchoolData> __Game_Prefabs_SchoolData_RO_ComponentLookup;

			// Token: 0x0400A08B RID: 41099
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x0400A08C RID: 41100
			[ReadOnly]
			public BufferLookup<Resources> __Game_Economy_Resources_RO_BufferLookup;

			// Token: 0x0400A08D RID: 41101
			[ReadOnly]
			public BufferLookup<ServiceFee> __Game_City_ServiceFee_RO_BufferLookup;

			// Token: 0x0400A08E RID: 41102
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x0400A08F RID: 41103
			[ReadOnly]
			public ComponentLookup<MovingAway> __Game_Agents_MovingAway_RO_ComponentLookup;

			// Token: 0x0400A090 RID: 41104
			[ReadOnly]
			public ComponentLookup<SchoolSeekerCooldown> __Game_Citizens_SchoolSeekerCooldown_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;
        }
	}
}
