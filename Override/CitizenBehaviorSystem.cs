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
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
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
	// Token: 0x020014E1 RID: 5345
	public partial class CitizenBehaviorSystem : GameSystemBase
	{
		// Token: 0x06006713 RID: 26387 RVA: 0x003561C9 File Offset: 0x003543C9
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x06006714 RID: 26388 RVA: 0x000464A6 File Offset: 0x000446A6
		public override int GetUpdateOffset(SystemUpdatePhase phase)
		{
			return 11;
		}

		// Token: 0x06006715 RID: 26389 RVA: 0x003760A8 File Offset: 0x003742A8
		public static float2 GetSleepTime(Entity entity, Citizen citizen, ref EconomyParameterData economyParameters, ref ComponentLookup<Worker> workers, ref ComponentLookup<Game.Citizens.Student> students)
		{
			CitizenAge age = citizen.GetAge();
			float2 @float = new float2(0.875f, 0.175f);
			float num = @float.y - @float.x;
			Unity.Mathematics.Random pseudoRandom = citizen.GetPseudoRandom(CitizenPseudoRandom.SleepOffset);
			@float += pseudoRandom.NextFloat(0f, 0.2f);
			if (age == CitizenAge.Elderly)
			{
				@float -= 0.05f;
			}
			if (age == CitizenAge.Child)
			{
				@float -= 0.1f;
			}
			if (age == CitizenAge.Teen)
			{
				@float += 0.05f;
			}
			@float = math.frac(@float);
			float2 float2;
			if (workers.HasComponent(entity))
			{
				float2 = WorkerSystem.GetTimeToWork(citizen, workers[entity], ref economyParameters, true);
			}
			else
			{
				if (!students.HasComponent(entity))
				{
					return @float;
				}
				float2 = StudentSystem.GetTimeToStudy(citizen, students[entity], ref economyParameters);
			}
			if (float2.x < float2.y)
			{
				if (@float.x > @float.y && float2.y > @float.x)
				{
					@float += float2.y - @float.x;
				}
				else if (@float.y > float2.x)
				{
					@float += 1f - (@float.y - float2.x);
				}
			}
			else
			{
				@float = new float2(float2.y, float2.y + num);
			}
			@float = math.frac(@float);
			return @float;
		}

		// Token: 0x06006716 RID: 26390 RVA: 0x003761F8 File Offset: 0x003743F8
		public static bool IsSleepTime(Entity entity, Citizen citizen, ref EconomyParameterData economyParameters, float normalizedTime, ref ComponentLookup<Worker> workers, ref ComponentLookup<Game.Citizens.Student> students)
		{
			float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(entity, citizen, ref economyParameters, ref workers, ref students);
			if (sleepTime.y < sleepTime.x)
			{
				return normalizedTime > sleepTime.x || normalizedTime < sleepTime.y;
			}
			return normalizedTime > sleepTime.x && normalizedTime < sleepTime.y;
		}

		// Token: 0x06006717 RID: 26391 RVA: 0x00376249 File Offset: 0x00374449
		public NativeQueue<Entity>.ParallelWriter GetCarReserveQueue(out JobHandle deps)
		{
			deps = this.m_CarReserveWriters;
			return this.m_ParallelCarReserveQueue;
		}

		// Token: 0x06006718 RID: 26392 RVA: 0x0037625D File Offset: 0x0037445D
		public void AddCarReserveWriter(JobHandle writer)
		{
			this.m_CarReserveWriters = JobHandle.CombineDependencies(this.m_CarReserveWriters, writer);
		}

		// Token: 0x06006719 RID: 26393 RVA: 0x00376274 File Offset: 0x00374474
		[Preserve]
		protected override void OnCreate()
		{
			base.OnCreate();
			this.m_SimulationSystem = base.World.GetOrCreateSystemManaged<SimulationSystem>();
			this.m_TimeSystem = base.World.GetOrCreateSystemManaged<TimeSystem>();
			this.m_EndFrameBarrier = base.World.GetOrCreateSystemManaged<EndFrameBarrier>();
			this.m_CarReserveQueue = new NativeQueue<Entity>(Allocator.Persistent);
			this.m_ParallelCarReserveQueue = this.m_CarReserveQueue.AsParallelWriter();
			this.m_EconomyParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<EconomyParameterData>() });
			this.m_LeisureParameterQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<LeisureParametersData>() });
			this.m_PopulationQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<Population>() });
			this.m_CitizenQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadWrite<Citizen>(),
				ComponentType.Exclude<TravelPurpose>(),
				ComponentType.Exclude<ResourceBuyer>(),
				ComponentType.ReadOnly<CurrentBuilding>(),
				ComponentType.ReadOnly<HouseholdMember>(),
				ComponentType.ReadOnly<UpdateFrame>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_OutsideConnectionQuery = base.GetEntityQuery(new ComponentType[]
			{
				ComponentType.ReadOnly<Game.Objects.OutsideConnection>(),
				ComponentType.Exclude<Game.Objects.ElectricityOutsideConnection>(),
				ComponentType.Exclude<Game.Objects.WaterPipeOutsideConnection>(),
				ComponentType.Exclude<Deleted>(),
				ComponentType.Exclude<Temp>()
			});
			this.m_TimeDataQuery = base.GetEntityQuery(new ComponentType[] { ComponentType.ReadOnly<TimeData>() });
			this.m_HouseholdArchetype = base.World.EntityManager.CreateArchetype(new ComponentType[]
			{
				ComponentType.ReadWrite<Household>(),
				ComponentType.ReadWrite<HouseholdNeed>(),
				ComponentType.ReadWrite<HouseholdCitizen>(),
				ComponentType.ReadWrite<TaxPayer>(),
				ComponentType.ReadWrite<Game.Economy.Resources>(),
				ComponentType.ReadWrite<UpdateFrame>(),
				ComponentType.ReadWrite<Created>()
			});
			base.RequireForUpdate(this.m_CitizenQuery);
			base.RequireForUpdate(this.m_EconomyParameterQuery);
			base.RequireForUpdate(this.m_LeisureParameterQuery);
			base.RequireForUpdate(this.m_TimeDataQuery);
			base.RequireForUpdate(this.m_PopulationQuery);
		}

		// Token: 0x0600671A RID: 26394 RVA: 0x003764C3 File Offset: 0x003746C3
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_CarReserveQueue.Dispose();
			base.OnDestroy();
		}

		// Token: 0x0600671B RID: 26395 RVA: 0x003764D8 File Offset: 0x003746D8
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			NativeQueue<Entity> nativeQueue = new NativeQueue<Entity>(Allocator.TempJob);
			NativeQueue<Entity> nativeQueue2 = new NativeQueue<Entity>(Allocator.TempJob);
			CitizenBehaviorSystem.CitizenAITickJob citizenAITickJob = default(CitizenBehaviorSystem.CitizenAITickJob);
			citizenAITickJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_UpdateFrameType = InternalCompilerInterface.GetSharedComponentTypeHandle<UpdateFrame>(ref this.__TypeHandle.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_TripType = InternalCompilerInterface.GetBufferTypeHandle<TripNeeded>(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_LeisureType = InternalCompilerInterface.GetComponentTypeHandle<Leisure>(ref this.__TypeHandle.__Game_Citizens_Leisure_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdNeeds = InternalCompilerInterface.GetComponentLookup<HouseholdNeed>(ref this.__TypeHandle.__Game_Citizens_HouseholdNeed_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Transforms = InternalCompilerInterface.GetComponentLookup<Game.Objects.Transform>(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_CarKeepers = InternalCompilerInterface.GetComponentLookup<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_PersonalCars = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_MovingAway = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Workers = InternalCompilerInterface.GetComponentLookup<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Students = InternalCompilerInterface.GetComponentLookup<Game.Citizens.Student>(ref this.__TypeHandle.__Game_Citizens_Student_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_InDangerData = InternalCompilerInterface.GetComponentLookup<InDanger>(ref this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Attendees = InternalCompilerInterface.GetBufferLookup<CoordinatedMeetingAttendee>(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Meetings = InternalCompilerInterface.GetComponentLookup<CoordinatedMeeting>(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_AttendingMeetings = InternalCompilerInterface.GetComponentLookup<AttendingMeeting>(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_MeetingDatas = InternalCompilerInterface.GetBufferLookup<HaveCoordinatedMeetingData>(ref this.__TypeHandle.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_BuildingStudents = InternalCompilerInterface.GetBufferLookup<Game.Buildings.Student>(ref this.__TypeHandle.__Game_Buildings_Student_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_PopulationData = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OutsideConnectionDatas = InternalCompilerInterface.GetComponentLookup<OutsideConnectionData>(ref this.__TypeHandle.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_CommuterHouseholds = InternalCompilerInterface.GetComponentLookup<CommuterHousehold>(ref this.__TypeHandle.__Game_Citizens_CommuterHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_CriminalData = InternalCompilerInterface.GetComponentLookup<Criminal>(ref this.__TypeHandle.__Game_Citizens_Criminal_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_LeisureSeekerCooldowns = InternalCompilerInterface.GetComponentLookup<LeisureSeekerCooldown>(ref this.__TypeHandle.__Game_Citizens_LeisureSeekerCooldown_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdArchetype = this.m_HouseholdArchetype;
			JobHandle jobHandle;
			citizenAITickJob.m_OutsideConnectionEntities = this.m_OutsideConnectionQuery.ToEntityListAsync(Allocator.TempJob, out jobHandle);
			citizenAITickJob.m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
			citizenAITickJob.m_LeisureParameters = this.m_LeisureParameterQuery.GetSingleton<LeisureParametersData>();
			citizenAITickJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
			citizenAITickJob.m_UpdateFrameIndex = updateFrameWithInterval;
			citizenAITickJob.m_SimulationFrame = this.m_SimulationSystem.frameIndex;
			citizenAITickJob.m_NormalizedTime = this.m_TimeSystem.normalizedTime;
			citizenAITickJob.m_TimeData = this.m_TimeDataQuery.GetSingleton<TimeData>();
			citizenAITickJob.m_PopulationEntity = this.m_PopulationQuery.GetSingletonEntity();
			citizenAITickJob.m_CarReserverQueue = this.m_ParallelCarReserveQueue;
			citizenAITickJob.m_MailSenderQueue = nativeQueue.AsParallelWriter();
			citizenAITickJob.m_SleepQueue = nativeQueue2.AsParallelWriter();
			citizenAITickJob.m_RandomSeed = RandomSeed.Next();
            citizenAITickJob.m_SmallCityJobs = SmallCityJobs.Create(ref base.CheckedStateRef);
            CitizenBehaviorSystem.CitizenAITickJob citizenAITickJob2 = citizenAITickJob;
			JobHandle jobHandle2 = citizenAITickJob2.ScheduleParallel(this.m_CitizenQuery, JobHandle.CombineDependencies(this.m_CarReserveWriters, JobHandle.CombineDependencies(base.Dependency, jobHandle)));
            SmallCityJobs.AddProducer(ref base.CheckedStateRef, jobHandle2);
            citizenAITickJob2.m_OutsideConnectionEntities.Dispose(jobHandle2);
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle2);
			this.AddCarReserveWriter(jobHandle2);
			CitizenBehaviorSystem.CitizenReserveHouseholdCarJob citizenReserveHouseholdCarJob = default(CitizenBehaviorSystem.CitizenReserveHouseholdCarJob);
			citizenReserveHouseholdCarJob.m_CarKeepers = InternalCompilerInterface.GetComponentLookup<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_HouseholdMembers = InternalCompilerInterface.GetComponentLookup<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_DistrictModifiers = InternalCompilerInterface.GetBufferLookup<DistrictModifier>(ref this.__TypeHandle.__Game_Areas_DistrictModifier_RO_BufferLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_CurrentDistricts = InternalCompilerInterface.GetComponentLookup<CurrentDistrict>(ref this.__TypeHandle.__Game_Areas_CurrentDistrict_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_PersonalCars = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_Citizens = InternalCompilerInterface.GetComponentLookup<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_BicycleOwners = InternalCompilerInterface.GetComponentLookup<BicycleOwner>(ref this.__TypeHandle.__Game_Citizens_BicycleOwner_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenReserveHouseholdCarJob.m_ReserverQueue = this.m_CarReserveQueue;
			JobHandle jobHandle3 = citizenReserveHouseholdCarJob.Schedule(JobHandle.CombineDependencies(jobHandle2, this.m_CarReserveWriters));
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle3);
			this.AddCarReserveWriter(jobHandle3);
			CitizenBehaviorSystem.CitizenTryCollectMailJob citizenTryCollectMailJob = default(CitizenBehaviorSystem.CitizenTryCollectMailJob);
			citizenTryCollectMailJob.m_CurrentBuildingData = InternalCompilerInterface.GetComponentLookup<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_PrefabRefData = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_SpawnableBuildingData = InternalCompilerInterface.GetComponentLookup<SpawnableBuildingData>(ref this.__TypeHandle.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_MailAccumulationData = InternalCompilerInterface.GetComponentLookup<MailAccumulationData>(ref this.__TypeHandle.__Game_Prefabs_MailAccumulationData_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_ServiceObjectData = InternalCompilerInterface.GetComponentLookup<ServiceObjectData>(ref this.__TypeHandle.__Game_Prefabs_ServiceObjectData_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_MailSenderData = InternalCompilerInterface.GetComponentLookup<MailSender>(ref this.__TypeHandle.__Game_Citizens_MailSender_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_MailProducerData = InternalCompilerInterface.GetComponentLookup<MailProducer>(ref this.__TypeHandle.__Game_Buildings_MailProducer_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenTryCollectMailJob.m_MailSenderQueue = nativeQueue;
			JobHandle jobHandle4 = citizenTryCollectMailJob.Schedule(jobHandle2);
			this.m_EndFrameBarrier.AddJobHandleForProducer(jobHandle4);
			nativeQueue.Dispose(jobHandle4);
			CitizenBehaviorSystem.CitizeSleepJob citizeSleepJob = default(CitizenBehaviorSystem.CitizeSleepJob);
			citizeSleepJob.m_CurrentBuildingData = InternalCompilerInterface.GetComponentLookup<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentLookup, ref base.CheckedStateRef);
			citizeSleepJob.m_CitizenPresenceData = InternalCompilerInterface.GetComponentLookup<CitizenPresence>(ref this.__TypeHandle.__Game_Buildings_CitizenPresence_RW_ComponentLookup, ref base.CheckedStateRef);
			citizeSleepJob.m_SleepQueue = nativeQueue2;
			JobHandle jobHandle5 = citizeSleepJob.Schedule(jobHandle2);
			nativeQueue2.Dispose(jobHandle5);
			base.Dependency = JobHandle.CombineDependencies(jobHandle3, jobHandle4, jobHandle5);
		}

		// Token: 0x0600671C RID: 26396 RVA: 0x00376CCC File Offset: 0x00374ECC
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			entityQueryBuilder.Dispose();
		}

		// Token: 0x0600671D RID: 26397 RVA: 0x00376CED File Offset: 0x00374EED
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x0600671E RID: 26398 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public CitizenBehaviorSystem()
		{
		}

		// Token: 0x0400921D RID: 37405
		public static readonly float kMaxPathfindCost = 17000f;

		// Token: 0x0400921E RID: 37406
		public static readonly int kMinLeisurePossibility = 80;

		// Token: 0x0400921F RID: 37407
		public static readonly uint kLeisureSeekerCooldownFrames = 20000U;

		// Token: 0x04009220 RID: 37408
		private JobHandle m_CarReserveWriters;

		// Token: 0x04009221 RID: 37409
		private EntityQuery m_CitizenQuery;

		// Token: 0x04009222 RID: 37410
		private EntityQuery m_OutsideConnectionQuery;

		// Token: 0x04009223 RID: 37411
		private EntityQuery m_EconomyParameterQuery;

		// Token: 0x04009224 RID: 37412
		private EntityQuery m_LeisureParameterQuery;

		// Token: 0x04009225 RID: 37413
		private EntityQuery m_TimeDataQuery;

		// Token: 0x04009226 RID: 37414
		private EntityQuery m_PopulationQuery;

		// Token: 0x04009227 RID: 37415
		private SimulationSystem m_SimulationSystem;

		// Token: 0x04009228 RID: 37416
		private TimeSystem m_TimeSystem;

		// Token: 0x04009229 RID: 37417
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x0400922A RID: 37418
		private EntityArchetype m_HouseholdArchetype;

		// Token: 0x0400922B RID: 37419
		private NativeQueue<Entity> m_CarReserveQueue;

		// Token: 0x0400922C RID: 37420
		private NativeQueue<Entity>.ParallelWriter m_ParallelCarReserveQueue;

		// Token: 0x0400922D RID: 37421
		private CitizenBehaviorSystem.TypeHandle __TypeHandle;

		// Token: 0x020014E2 RID: 5346
		[BurstCompile]
		private struct CitizenReserveHouseholdCarJob : IJob
		{
			// Token: 0x06006720 RID: 26400 RVA: 0x00376D30 File Offset: 0x00374F30
			public void Execute()
			{
				Entity entity;
				while (this.m_ReserverQueue.TryDequeue(out entity))
				{
					Citizen citizen;
					HouseholdMember householdMember;
					BicycleOwner bicycleOwner;
					Game.Vehicles.PersonalCar personalCar;
					if (this.m_Citizens.TryGetComponent(entity, out citizen) && citizen.GetAge() != CitizenAge.Child && !this.m_CarKeepers.IsComponentEnabled(entity) && this.m_HouseholdMembers.TryGetComponent(entity, out householdMember) && (!this.m_BicycleOwners.TryGetEnabledComponent(entity, out bicycleOwner) || !this.m_PersonalCars.TryGetComponent(bicycleOwner.m_Bicycle, out personalCar) || (personalCar.m_State & PersonalCarFlags.HomeTarget) != (PersonalCarFlags)0U))
					{
						float num = 100f;
						PropertyRenter propertyRenter;
						CurrentDistrict currentDistrict;
						DynamicBuffer<DistrictModifier> dynamicBuffer;
						if (this.m_PropertyRenters.TryGetComponent(householdMember.m_Household, out propertyRenter) && this.m_CurrentDistricts.TryGetComponent(propertyRenter.m_Property, out currentDistrict) && this.m_DistrictModifiers.TryGetBuffer(currentDistrict.m_District, out dynamicBuffer))
						{
							AreaUtils.ApplyModifier(ref num, dynamicBuffer, DistrictModifierType.CarReserveProbability);
						}
						if (citizen.GetPseudoRandom(CitizenPseudoRandom.CarProbability).NextFloat(100f) <= num)
						{
							Entity @null = Entity.Null;
							if (HouseholdBehaviorSystem.GetFreeCar(householdMember.m_Household, this.m_OwnedVehicles, this.m_PersonalCars, ref @null))
							{
								this.m_CarKeepers.SetComponentEnabled(entity, true);
								this.m_CarKeepers[entity] = new CarKeeper
								{
									m_Car = @null
								};
								Game.Vehicles.PersonalCar personalCar2 = this.m_PersonalCars[@null];
								personalCar2.m_Keeper = entity;
								this.m_PersonalCars[@null] = personalCar2;
							}
						}
					}
				}
			}

			// Token: 0x0400922E RID: 37422
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x0400922F RID: 37423
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCars;

			// Token: 0x04009230 RID: 37424
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x04009231 RID: 37425
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x04009232 RID: 37426
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x04009233 RID: 37427
			[ReadOnly]
			public BufferLookup<DistrictModifier> m_DistrictModifiers;

			// Token: 0x04009234 RID: 37428
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> m_CurrentDistricts;

			// Token: 0x04009235 RID: 37429
			[ReadOnly]
			public ComponentLookup<Citizen> m_Citizens;

			// Token: 0x04009236 RID: 37430
			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwners;

			// Token: 0x04009237 RID: 37431
			public NativeQueue<Entity> m_ReserverQueue;
		}

		// Token: 0x020014E3 RID: 5347
		[BurstCompile]
		private struct CitizenTryCollectMailJob : IJob
		{
			// Token: 0x06006721 RID: 26401 RVA: 0x00376EA8 File Offset: 0x003750A8
			public void Execute()
			{
				Entity entity;
				while (this.m_MailSenderQueue.TryDequeue(out entity))
				{
					CurrentBuilding currentBuilding;
					MailProducer mailProducer;
					if (this.m_CurrentBuildingData.TryGetComponent(entity, out currentBuilding) && this.m_MailProducerData.TryGetComponent(currentBuilding.m_CurrentBuilding, out mailProducer) && mailProducer.m_SendingMail >= 15 && !this.RequireCollect(this.m_PrefabRefData[currentBuilding.m_CurrentBuilding].m_Prefab))
					{
						bool flag = this.m_MailSenderData.IsComponentEnabled(entity);
						MailSender mailSender = (flag ? this.m_MailSenderData[entity] : default(MailSender));
						int num = math.min((int)mailProducer.m_SendingMail, (int)(100 - mailSender.m_Amount));
						if (num > 0)
						{
							mailSender.m_Amount = (ushort)((int)mailSender.m_Amount + num);
							mailProducer.m_SendingMail = (ushort)((int)mailProducer.m_SendingMail - num);
							this.m_MailProducerData[currentBuilding.m_CurrentBuilding] = mailProducer;
							if (!flag)
							{
								this.m_MailSenderData.SetComponentEnabled(entity, true);
							}
							this.m_MailSenderData[entity] = mailSender;
						}
					}
				}
			}

			// Token: 0x06006722 RID: 26402 RVA: 0x00376FB8 File Offset: 0x003751B8
			private bool RequireCollect(Entity prefab)
			{
				if (this.m_SpawnableBuildingData.HasComponent(prefab))
				{
					SpawnableBuildingData spawnableBuildingData = this.m_SpawnableBuildingData[prefab];
					if (this.m_MailAccumulationData.HasComponent(spawnableBuildingData.m_ZonePrefab))
					{
						return this.m_MailAccumulationData[spawnableBuildingData.m_ZonePrefab].m_RequireCollect;
					}
				}
				else if (this.m_ServiceObjectData.HasComponent(prefab))
				{
					ServiceObjectData serviceObjectData = this.m_ServiceObjectData[prefab];
					if (this.m_MailAccumulationData.HasComponent(serviceObjectData.m_Service))
					{
						return this.m_MailAccumulationData[serviceObjectData.m_Service].m_RequireCollect;
					}
				}
				return false;
			}

			// Token: 0x04009238 RID: 37432
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			// Token: 0x04009239 RID: 37433
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			// Token: 0x0400923A RID: 37434
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingData;

			// Token: 0x0400923B RID: 37435
			[ReadOnly]
			public ComponentLookup<MailAccumulationData> m_MailAccumulationData;

			// Token: 0x0400923C RID: 37436
			[ReadOnly]
			public ComponentLookup<ServiceObjectData> m_ServiceObjectData;

			// Token: 0x0400923D RID: 37437
			public ComponentLookup<MailSender> m_MailSenderData;

			// Token: 0x0400923E RID: 37438
			public ComponentLookup<MailProducer> m_MailProducerData;

			// Token: 0x0400923F RID: 37439
			public NativeQueue<Entity> m_MailSenderQueue;
		}

		// Token: 0x020014E4 RID: 5348
		[BurstCompile]
		private struct CitizeSleepJob : IJob
		{
			// Token: 0x06006723 RID: 26403 RVA: 0x00377050 File Offset: 0x00375250
			public void Execute()
			{
				Entity entity;
				while (this.m_SleepQueue.TryDequeue(out entity))
				{
					if (this.m_CurrentBuildingData.HasComponent(entity))
					{
						CurrentBuilding currentBuilding = this.m_CurrentBuildingData[entity];
						if (this.m_CitizenPresenceData.HasComponent(currentBuilding.m_CurrentBuilding))
						{
							CitizenPresence citizenPresence = this.m_CitizenPresenceData[currentBuilding.m_CurrentBuilding];
							citizenPresence.m_Delta = (sbyte)math.max(-127, (int)(citizenPresence.m_Delta - 1));
							this.m_CitizenPresenceData[currentBuilding.m_CurrentBuilding] = citizenPresence;
						}
					}
				}
			}

			// Token: 0x04009240 RID: 37440
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			// Token: 0x04009241 RID: 37441
			public ComponentLookup<CitizenPresence> m_CitizenPresenceData;

			// Token: 0x04009242 RID: 37442
			public NativeQueue<Entity> m_SleepQueue;
		}

		// Token: 0x020014E5 RID: 5349
		[BurstCompile]
		private struct CitizenAITickJob : IJobChunk
		{
            public SmallCityJobs m_SmallCityJobs;
            // Token: 0x06006724 RID: 26404 RVA: 0x003770D8 File Offset: 0x003752D8
            private bool CheckSleep(int index, Entity entity, ref Citizen citizen, Entity currentBuilding, Entity household, Entity home, DynamicBuffer<TripNeeded> trips, ref EconomyParameterData economyParameters, ref Unity.Mathematics.Random random)
			{
				if (home != Entity.Null && CitizenBehaviorSystem.IsSleepTime(entity, citizen, ref economyParameters, this.m_NormalizedTime, ref this.m_Workers, ref this.m_Students))
				{
					if (currentBuilding == home)
					{
						this.m_CommandBuffer.AddComponent<TravelPurpose>(index, entity, new TravelPurpose
						{
							m_Purpose = Purpose.Sleeping
						});
						this.m_SleepQueue.Enqueue(entity);
						this.ReleaseCar(index, entity);
					}
					else
					{
						this.GoHome(entity, home, trips, currentBuilding);
					}
					return true;
				}
				return false;
			}

			// Token: 0x06006725 RID: 26405 RVA: 0x00377164 File Offset: 0x00375364
			private void GoHome(Entity entity, Entity target, DynamicBuffer<TripNeeded> trips, Entity currentBuilding)
			{
				if (target == Entity.Null)
				{
					return;
				}
				if (currentBuilding == target)
				{
					return;
				}
				if (!this.m_CarKeepers.IsComponentEnabled(entity))
				{
					this.m_CarReserverQueue.Enqueue(entity);
				}
				this.m_MailSenderQueue.Enqueue(entity);
				TripNeeded tripNeeded = new TripNeeded
				{
					m_TargetAgent = target,
					m_Purpose = Purpose.GoingHome,
					m_Priority = 128
				};
				trips.Add(tripNeeded);
			}

			// Token: 0x06006726 RID: 26406 RVA: 0x003771E0 File Offset: 0x003753E0
			private void GoToOutsideConnection(Entity entity, Entity household, Entity currentBuilding, Entity targetBuilding, ref Citizen citizen, DynamicBuffer<TripNeeded> trips, Purpose purpose, ref Unity.Mathematics.Random random)
			{
				if (purpose == Purpose.MovingAway)
				{
					for (int i = 0; i < trips.Length; i++)
					{
						if (trips[i].m_Purpose == Purpose.MovingAway)
						{
							return;
						}
					}
				}
				if (!this.m_OutsideConnections.HasComponent(currentBuilding))
				{
					if (!this.m_CarKeepers.IsComponentEnabled(entity))
					{
						this.m_CarReserverQueue.Enqueue(entity);
					}
					this.m_MailSenderQueue.Enqueue(entity);
					if (targetBuilding == Entity.Null)
					{
						OutsideConnectionTransferType outsideConnectionTransferType = OutsideConnectionTransferType.Train | OutsideConnectionTransferType.Air | OutsideConnectionTransferType.Ship;
						if (this.m_OwnedVehicles.HasBuffer(household) && this.m_OwnedVehicles[household].Length > 0)
						{
							outsideConnectionTransferType |= OutsideConnectionTransferType.Road;
						}
						BuildingUtils.GetRandomOutsideConnectionByTransferType(ref this.m_OutsideConnectionEntities, ref this.m_OutsideConnectionDatas, ref this.m_Prefabs, random, outsideConnectionTransferType, out targetBuilding);
					}
					if (targetBuilding == Entity.Null && this.m_OutsideConnectionEntities.Length != 0)
					{
						int num = random.NextInt(this.m_OutsideConnectionEntities.Length);
						targetBuilding = this.m_OutsideConnectionEntities[num];
					}
					trips.Add(new TripNeeded
					{
						m_TargetAgent = targetBuilding,
						m_Purpose = purpose,
						m_Priority = 128
					});
					return;
				}
				if (purpose == Purpose.MovingAway)
				{
					citizen.m_State |= CitizenFlags.MovingAwayReachOC;
				}
			}

			// Token: 0x06006727 RID: 26407 RVA: 0x00377328 File Offset: 0x00375528
			private void GoShopping(int chunkIndex, Entity citizen, Entity household, HouseholdNeed need, float3 position)
			{
				if (!this.m_CarKeepers.IsComponentEnabled(citizen))
				{
					this.m_CarReserverQueue.Enqueue(citizen);
				}
				this.m_MailSenderQueue.Enqueue(citizen);
				this.m_CommandBuffer.AddComponent<ResourceBuyer>(chunkIndex, citizen, new ResourceBuyer
				{
					m_Payer = household,
					m_Flags = SetupTargetFlags.Commercial,
					m_Location = position,
					m_ResourceNeeded = need.m_Resource,
					m_AmountNeeded = need.m_Amount
				});
			}

			// Token: 0x06006728 RID: 26408 RVA: 0x003773A6 File Offset: 0x003755A6
			private float GetTimeLeftUntilInterval(float2 interval)
			{
				if (this.m_NormalizedTime >= interval.x)
				{
					return 1f - this.m_NormalizedTime + interval.x;
				}
				return interval.x - this.m_NormalizedTime;
			}

			// Token: 0x06006729 RID: 26409 RVA: 0x003773D8 File Offset: 0x003755D8
			private bool DoLeisure(int chunkIndex, Entity citizenEntity, Entity householdEntity, Entity currentBuilding, Entity homeEntity, bool isTourist, ref Citizen citizenData, int population, ref Unity.Mathematics.Random random, ref EconomyParameterData economyParameters)
			{
				bool flag = CitizenUtils.HasMovedIn(householdEntity, this.m_Households) && homeEntity == Entity.Null;
				if (isTourist)
				{
					if (this.m_OutsideConnections.HasComponent(currentBuilding) && this.m_TouristHouseholds[householdEntity].m_Hotel != Entity.Null)
					{
						return false;
					}
				}
				else if (!flag)
				{
					LeisureSeekerCooldown leisureSeekerCooldown;
					if (this.m_LeisureSeekerCooldowns.TryGetComponent(citizenEntity, out leisureSeekerCooldown) && this.m_SimulationFrame < leisureSeekerCooldown.m_SimulationFrame + CitizenBehaviorSystem.kLeisureSeekerCooldownFrames)
					{
						return false;
					}
					int num = (int)(128 - citizenData.m_LeisureCounter);
					if (this.m_OutsideConnections.HasComponent(currentBuilding) || random.NextInt(this.m_LeisureParameters.m_LeisureRandomFactor) > num)
					{
						return false;
					}
				}
				int num2 = math.min(CitizenBehaviorSystem.kMinLeisurePossibility, Mathf.RoundToInt(200f / math.max(1f, math.sqrt(economyParameters.m_TrafficReduction * (float)population))));
				if (!isTourist && !flag && random.NextInt(100) > num2)
				{
					citizenData.m_LeisureCounter = byte.MaxValue;
					return true;
				}
				float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(citizenEntity, citizenData, ref economyParameters, ref this.m_Workers, ref this.m_Students);
				float num3 = this.GetTimeLeftUntilInterval(sleepTime);
				if (this.m_Workers.HasComponent(citizenEntity))
				{
					Worker worker = this.m_Workers[citizenEntity];
					citizenData.m_UnemploymentTimeCounter = 0f;
					float2 timeToWork = WorkerSystem.GetTimeToWork(citizenData, worker, ref economyParameters, true);
					num3 = math.min(num3, this.GetTimeLeftUntilInterval(timeToWork));
				}
				else if (this.m_Students.HasComponent(citizenEntity))
				{
					citizenData.m_UnemploymentTimeCounter = 0f;
					Game.Citizens.Student student = this.m_Students[citizenEntity];
					float2 timeToStudy = StudentSystem.GetTimeToStudy(citizenData, student, ref economyParameters);
					num3 = math.min(num3, this.GetTimeLeftUntilInterval(timeToStudy));
				}
				if (isTourist)
				{
					citizenData.m_LeisureCounter = 0;
				}
				uint num4 = (uint)(num3 * 262144f);
				Leisure leisure = new Leisure
				{
					m_LastPossibleFrame = this.m_SimulationFrame + num4
				};
				this.m_CommandBuffer.AddComponent<Leisure>(chunkIndex, citizenEntity, leisure);
				return true;
			}

			// Token: 0x0600672A RID: 26410 RVA: 0x003775E4 File Offset: 0x003757E4
			private void ReleaseCar(int chunkIndex, Entity citizen)
			{
				if (this.m_CarKeepers.IsComponentEnabled(citizen))
				{
					Entity car = this.m_CarKeepers[citizen].m_Car;
					if (this.m_PersonalCars.HasComponent(car))
					{
						Game.Vehicles.PersonalCar personalCar = this.m_PersonalCars[car];
						personalCar.m_Keeper = Entity.Null;
						this.m_PersonalCars[car] = personalCar;
					}
					this.m_CommandBuffer.SetComponentEnabled<CarKeeper>(chunkIndex, citizen, false);
				}
			}

			// Token: 0x0600672B RID: 26411 RVA: 0x00377654 File Offset: 0x00375854
			private bool AttendMeeting(int chunkIndex, Entity entity, ref Citizen citizen, Entity household, Entity currentBuilding, DynamicBuffer<TripNeeded> trips, ref Unity.Mathematics.Random random)
			{
				if (!this.m_CarKeepers.IsComponentEnabled(entity))
				{
					this.m_CarReserverQueue.Enqueue(entity);
				}
				Entity meeting = this.m_AttendingMeetings[entity].m_Meeting;
				if (this.m_Attendees.HasBuffer(meeting) && this.m_Meetings.HasComponent(meeting))
				{
					CoordinatedMeeting coordinatedMeeting = this.m_Meetings[meeting];
					if (this.m_Prefabs.HasComponent(meeting) && coordinatedMeeting.m_Status != MeetingStatus.Done)
					{
						HaveCoordinatedMeetingData haveCoordinatedMeetingData = this.m_MeetingDatas[this.m_Prefabs[meeting].m_Prefab][coordinatedMeeting.m_Phase];
						DynamicBuffer<CoordinatedMeetingAttendee> dynamicBuffer = this.m_Attendees[meeting];
						if (coordinatedMeeting.m_Status == MeetingStatus.Waiting && coordinatedMeeting.m_Target == Entity.Null)
						{
							if (dynamicBuffer.Length > 0 && dynamicBuffer[0].m_Attendee == entity)
							{
								if (haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose == Purpose.Shopping)
								{
									float3 position = this.m_Transforms[currentBuilding].m_Position;
									this.GoShopping(chunkIndex, entity, household, new HouseholdNeed
									{
										m_Resource = haveCoordinatedMeetingData.m_TravelPurpose.m_Resource,
										m_Amount = haveCoordinatedMeetingData.m_TravelPurpose.m_Data
									}, position);
									return true;
								}
								if (haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose == Purpose.Traveling)
								{
									Citizen citizen2 = default(Citizen);
									this.GoToOutsideConnection(entity, household, currentBuilding, Entity.Null, ref citizen2, trips, haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose, ref random);
								}
								else
								{
									if (haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose != Purpose.GoingHome)
									{
										trips.Add(new TripNeeded
										{
											m_Purpose = haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose,
											m_Resource = haveCoordinatedMeetingData.m_TravelPurpose.m_Resource,
											m_Data = haveCoordinatedMeetingData.m_TravelPurpose.m_Data,
											m_TargetAgent = default(Entity),
											m_Priority = 128
										});
										return true;
									}
									if (this.m_PropertyRenters.HasComponent(household))
									{
										coordinatedMeeting.m_Target = this.m_PropertyRenters[household].m_Property;
										this.m_Meetings[meeting] = coordinatedMeeting;
										this.GoHome(entity, this.m_PropertyRenters[household].m_Property, trips, currentBuilding);
									}
								}
							}
						}
						else if (coordinatedMeeting.m_Status == MeetingStatus.Waiting || coordinatedMeeting.m_Status == MeetingStatus.Traveling)
						{
							for (int i = 0; i < dynamicBuffer.Length; i++)
							{
								if (dynamicBuffer[i].m_Attendee == entity)
								{
									if (coordinatedMeeting.m_Target != Entity.Null && currentBuilding != coordinatedMeeting.m_Target && (!this.m_PropertyRenters.HasComponent(coordinatedMeeting.m_Target) || this.m_PropertyRenters[coordinatedMeeting.m_Target].m_Property != currentBuilding))
									{
										trips.Add(new TripNeeded
										{
											m_Purpose = haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose,
											m_Resource = haveCoordinatedMeetingData.m_TravelPurpose.m_Resource,
											m_Data = haveCoordinatedMeetingData.m_TravelPurpose.m_Data,
											m_TargetAgent = coordinatedMeeting.m_Target,
											m_Priority = 128
										});
									}
									return true;
								}
							}
							this.m_CommandBuffer.RemoveComponent<AttendingMeeting>(chunkIndex, entity);
							return false;
						}
					}
					return coordinatedMeeting.m_Status != MeetingStatus.Done;
				}
				this.m_CommandBuffer.RemoveComponent<AttendingMeeting>(chunkIndex, entity);
				return false;
			}

			// Token: 0x0600672C RID: 26412 RVA: 0x003779EC File Offset: 0x00375BEC
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				if (chunk.GetSharedComponent<UpdateFrame>(this.m_UpdateFrameType).m_Index != this.m_UpdateFrameIndex)
				{
					return;
				}
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray<HouseholdMember>(ref this.m_HouseholdMemberType);
				NativeArray<CurrentBuilding> nativeArray4 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				NativeArray<HealthProblem> nativeArray5 = chunk.GetNativeArray<HealthProblem>(ref this.m_HealthProblemType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripType);
				bool flag = nativeArray5.Length > 0;
				int population = this.m_PopulationData[this.m_PopulationEntity].m_Population;
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Citizen citizen = nativeArray2[i];
					if (!flag || !CitizenUtils.IsDead(nativeArray5[i]))
					{
						Entity entity = nativeArray3[i].m_Household;
						Entity entity2 = nativeArray[i];
						bool flag2 = this.m_TouristHouseholds.HasComponent(entity);
						bool flag3 = this.m_HomelessHouseholds.HasComponent(entity);
						Criminal criminal;
						if (!this.m_CriminalData.TryGetComponent(entity2, out criminal) || (criminal.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested | CriminalFlags.Sentenced)) == (CriminalFlags)0)
						{
							DynamicBuffer<TripNeeded> dynamicBuffer = bufferAccessor[i];
							if (entity == Entity.Null)
							{
								entity = this.m_CommandBuffer.CreateEntity(unfilteredChunkIndex, this.m_HouseholdArchetype);
								this.m_CommandBuffer.SetComponent<HouseholdMember>(unfilteredChunkIndex, entity2, new HouseholdMember
								{
									m_Household = entity
								});
								this.m_CommandBuffer.SetBuffer<HouseholdCitizen>(unfilteredChunkIndex, entity).Add(new HouseholdCitizen
								{
									m_Citizen = entity2
								});
								global::UnityEngine.Debug.LogWarning(string.Format("Citizen:{0} don't have valid household", entity2.Index));
							}
							else if (!this.m_Households.HasComponent(entity))
							{
								this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
							}
							else
							{
								Entity currentBuilding = nativeArray4[i].m_CurrentBuilding;
								if (currentBuilding == Entity.Null && this.m_MovingAway.HasComponent(entity))
								{
									this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity, default(Deleted));
								}
								else if (this.m_Transforms.HasComponent(currentBuilding) && (!this.m_InDangerData.HasComponent(currentBuilding) || (this.m_InDangerData[currentBuilding].m_Flags & DangerFlags.StayIndoors) == (DangerFlags)0U))
								{
									bool flag4 = (citizen.m_State & CitizenFlags.Commuter) > CitizenFlags.None;
									CitizenAge age = citizen.GetAge();
									if (flag4 && (age == CitizenAge.Elderly || age == CitizenAge.Child))
									{
										this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
									}
									MovingAway movingAway;
									if ((citizen.m_State & CitizenFlags.MovingAwayReachOC) != CitizenFlags.None)
									{
										this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
									}
									else if (this.m_MovingAway.TryGetComponent(entity, out movingAway))
									{
										this.GoToOutsideConnection(entity2, entity, currentBuilding, movingAway.m_Target, ref citizen, dynamicBuffer, Purpose.MovingAway, ref random);
										if (chunk.Has<Leisure>(ref this.m_LeisureType))
										{
											this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
										}
										if (this.m_Workers.HasComponent(entity2))
										{
											this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity2);
										}
										if (this.m_Students.HasComponent(entity2))
										{
											if (this.m_BuildingStudents.HasBuffer(this.m_Students[entity2].m_School))
											{
												this.m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex, this.m_Students[entity2].m_School);
											}
											this.m_CommandBuffer.RemoveComponent<Game.Citizens.Student>(unfilteredChunkIndex, entity2);
										}
										nativeArray2[i] = citizen;
									}
									else
									{
										Entity entity3 = Entity.Null;
										if (this.m_PropertyRenters.HasComponent(entity))
										{
											entity3 = this.m_PropertyRenters[entity].m_Property;
										}
										else if (flag3)
										{
											entity3 = this.m_HomelessHouseholds[entity].m_TempHome;
										}
										else if (flag2)
										{
											Entity hotel = this.m_TouristHouseholds[entity].m_Hotel;
											if (this.m_PropertyRenters.HasComponent(hotel))
											{
												entity3 = this.m_PropertyRenters[hotel].m_Property;
											}
										}
										else if (flag4)
										{
											if (this.m_OutsideConnections.HasComponent(currentBuilding))
											{
												entity3 = currentBuilding;
											}
											else
											{
												CommuterHousehold commuterHousehold;
												if (this.m_CommuterHouseholds.TryGetComponent(entity, out commuterHousehold))
												{
													entity3 = commuterHousehold.m_OriginalFrom;
												}
												if (entity3 == Entity.Null)
												{
													entity3 = this.m_OutsideConnectionEntities[random.NextInt(this.m_OutsideConnectionEntities.Length)];
												}
											}
										}
										if (flag)
										{
											if (chunk.Has<Leisure>(ref this.m_LeisureType))
											{
												this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
											}
										}
										else if (!this.m_AttendingMeetings.HasComponent(entity2) || !this.AttendMeeting(unfilteredChunkIndex, entity2, ref citizen, entity, currentBuilding, dynamicBuffer, ref random))
										{
											if ((this.m_Workers.HasComponent(entity2) && !m_SmallCityJobs.IsTodayOffDay(citizen, ref this.m_EconomyParameters, this.m_SimulationFrame, this.m_TimeData, population) && WorkerSystem.IsTimeToWork(citizen, this.m_Workers[entity2], ref this.m_EconomyParameters, this.m_NormalizedTime)) || (this.m_Students.HasComponent(entity2) && StudentSystem.IsTimeToStudy(citizen, this.m_Students[entity2], ref this.m_EconomyParameters, this.m_NormalizedTime, this.m_SimulationFrame, this.m_TimeData, population)))
											{
												if (chunk.Has<Leisure>(ref this.m_LeisureType))
												{
													this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
												}
											}
											else if (this.CheckSleep(i, entity2, ref citizen, currentBuilding, entity, entity3, dynamicBuffer, ref this.m_EconomyParameters, ref random))
											{
												if (chunk.Has<Leisure>(ref this.m_LeisureType))
												{
													this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
												}
											}
											else
											{
												if (age == CitizenAge.Adult || age == CitizenAge.Elderly)
												{
													HouseholdNeed householdNeed = this.m_HouseholdNeeds[entity];
													if (householdNeed.m_Resource != Resource.NoResource && this.m_Transforms.HasComponent(currentBuilding))
													{
														this.GoShopping(unfilteredChunkIndex, entity2, entity, householdNeed, this.m_Transforms[currentBuilding].m_Position);
														householdNeed.m_Resource = Resource.NoResource;
														this.m_HouseholdNeeds[entity] = householdNeed;
														if (chunk.Has<Leisure>(ref this.m_LeisureType))
														{
															this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
															goto IL_06C1;
														}
														goto IL_06C1;
													}
												}
												if (!chunk.Has<Leisure>(ref this.m_LeisureType) && this.DoLeisure(unfilteredChunkIndex, entity2, entity, currentBuilding, entity3, flag2, ref citizen, population, ref random, ref this.m_EconomyParameters))
												{
													nativeArray2[i] = citizen;
												}
												else if (!chunk.Has<Leisure>(ref this.m_LeisureType))
												{
													if (currentBuilding != entity3)
													{
														this.GoHome(entity2, entity3, dynamicBuffer, currentBuilding);
													}
													else
													{
														this.ReleaseCar(unfilteredChunkIndex, entity2);
													}
												}
											}
										}
									}
								}
							}
						}
					}
					IL_06C1:;
				}
			}

			// Token: 0x0600672D RID: 26413 RVA: 0x003780CE File Offset: 0x003762CE
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x04009243 RID: 37443
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x04009244 RID: 37444
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x04009245 RID: 37445
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			// Token: 0x04009246 RID: 37446
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x04009247 RID: 37447
			[ReadOnly]
			public SharedComponentTypeHandle<UpdateFrame> m_UpdateFrameType;

			// Token: 0x04009248 RID: 37448
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x04009249 RID: 37449
			public BufferTypeHandle<TripNeeded> m_TripType;

			// Token: 0x0400924A RID: 37450
			[ReadOnly]
			public ComponentTypeHandle<Leisure> m_LeisureType;

			// Token: 0x0400924B RID: 37451
			[NativeDisableParallelForRestriction]
			public ComponentLookup<HouseholdNeed> m_HouseholdNeeds;

			// Token: 0x0400924C RID: 37452
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x0400924D RID: 37453
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x0400924E RID: 37454
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			// Token: 0x0400924F RID: 37455
			[ReadOnly]
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x04009250 RID: 37456
			[NativeDisableParallelForRestriction]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCars;

			// Token: 0x04009251 RID: 37457
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAway;

			// Token: 0x04009252 RID: 37458
			[ReadOnly]
			public ComponentLookup<Worker> m_Workers;

			// Token: 0x04009253 RID: 37459
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> m_Students;

			// Token: 0x04009254 RID: 37460
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x04009255 RID: 37461
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

			// Token: 0x04009256 RID: 37462
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x04009257 RID: 37463
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;

			// Token: 0x04009258 RID: 37464
			[ReadOnly]
			public ComponentLookup<InDanger> m_InDangerData;

			// Token: 0x04009259 RID: 37465
			[ReadOnly]
			public ComponentLookup<AttendingMeeting> m_AttendingMeetings;

			// Token: 0x0400925A RID: 37466
			[NativeDisableParallelForRestriction]
			public ComponentLookup<CoordinatedMeeting> m_Meetings;

			// Token: 0x0400925B RID: 37467
			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> m_Attendees;

			// Token: 0x0400925C RID: 37468
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> m_MeetingDatas;

			// Token: 0x0400925D RID: 37469
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x0400925E RID: 37470
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> m_BuildingStudents;

			// Token: 0x0400925F RID: 37471
			[ReadOnly]
			public ComponentLookup<Population> m_PopulationData;

			// Token: 0x04009260 RID: 37472
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x04009261 RID: 37473
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> m_CommuterHouseholds;

			// Token: 0x04009262 RID: 37474
			[ReadOnly]
			public ComponentLookup<Criminal> m_CriminalData;

			// Token: 0x04009263 RID: 37475
			[ReadOnly]
			public ComponentLookup<LeisureSeekerCooldown> m_LeisureSeekerCooldowns;

			// Token: 0x04009264 RID: 37476
			[ReadOnly]
			public EntityArchetype m_HouseholdArchetype;

			// Token: 0x04009265 RID: 37477
			[ReadOnly]
			public NativeList<Entity> m_OutsideConnectionEntities;

			// Token: 0x04009266 RID: 37478
			[ReadOnly]
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x04009267 RID: 37479
			[ReadOnly]
			public LeisureParametersData m_LeisureParameters;

			// Token: 0x04009268 RID: 37480
			public uint m_UpdateFrameIndex;

			// Token: 0x04009269 RID: 37481
			public float m_NormalizedTime;

			// Token: 0x0400926A RID: 37482
			public uint m_SimulationFrame;

			// Token: 0x0400926B RID: 37483
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x0400926C RID: 37484
			public NativeQueue<Entity>.ParallelWriter m_CarReserverQueue;

			// Token: 0x0400926D RID: 37485
			public NativeQueue<Entity>.ParallelWriter m_MailSenderQueue;

			// Token: 0x0400926E RID: 37486
			public NativeQueue<Entity>.ParallelWriter m_SleepQueue;

			// Token: 0x0400926F RID: 37487
			public TimeData m_TimeData;

			// Token: 0x04009270 RID: 37488
			public Entity m_PopulationEntity;

			// Token: 0x04009271 RID: 37489
			public RandomSeed m_RandomSeed;
		}

		// Token: 0x020014E6 RID: 5350
		private struct TypeHandle
		{
			// Token: 0x0600672E RID: 26414 RVA: 0x003780DC File Offset: 0x003762DC
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(true);
				this.__Game_Simulation_UpdateFrame_SharedComponentTypeHandle = state.GetSharedComponentTypeHandle<UpdateFrame>();
				this.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(true);
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>(false);
				this.__Game_Citizens_Leisure_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Leisure>(true);
				this.__Game_Citizens_HouseholdNeed_RW_ComponentLookup = state.GetComponentLookup<HouseholdNeed>(false);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(true);
				this.__Game_Citizens_CarKeeper_RO_ComponentLookup = state.GetComponentLookup<CarKeeper>(true);
				this.__Game_Vehicles_PersonalCar_RW_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(false);
				this.__Game_Agents_MovingAway_RO_ComponentLookup = state.GetComponentLookup<MovingAway>(true);
				this.__Game_Citizens_Worker_RO_ComponentLookup = state.GetComponentLookup<Worker>(true);
				this.__Game_Citizens_Student_RO_ComponentLookup = state.GetComponentLookup<Game.Citizens.Student>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Events_InDanger_RO_ComponentLookup = state.GetComponentLookup<InDanger>(true);
				this.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup = state.GetBufferLookup<CoordinatedMeetingAttendee>(true);
				this.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup = state.GetComponentLookup<CoordinatedMeeting>(false);
				this.__Game_Citizens_AttendingMeeting_RO_ComponentLookup = state.GetComponentLookup<AttendingMeeting>(true);
				this.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup = state.GetBufferLookup<HaveCoordinatedMeetingData>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Buildings_Student_RO_BufferLookup = state.GetBufferLookup<Game.Buildings.Student>(true);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
				this.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup = state.GetComponentLookup<OutsideConnectionData>(true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
				this.__Game_Citizens_CommuterHousehold_RO_ComponentLookup = state.GetComponentLookup<CommuterHousehold>(true);
				this.__Game_Citizens_Criminal_RO_ComponentLookup = state.GetComponentLookup<Criminal>(true);
				this.__Game_Citizens_LeisureSeekerCooldown_RO_ComponentLookup = state.GetComponentLookup<LeisureSeekerCooldown>(true);
				this.__Game_Citizens_CarKeeper_RW_ComponentLookup = state.GetComponentLookup<CarKeeper>(false);
				this.__Game_Citizens_HouseholdMember_RO_ComponentLookup = state.GetComponentLookup<HouseholdMember>(true);
				this.__Game_Areas_DistrictModifier_RO_BufferLookup = state.GetBufferLookup<DistrictModifier>(true);
				this.__Game_Areas_CurrentDistrict_RO_ComponentLookup = state.GetComponentLookup<CurrentDistrict>(true);
				this.__Game_Citizens_Citizen_RO_ComponentLookup = state.GetComponentLookup<Citizen>(true);
				this.__Game_Citizens_BicycleOwner_RO_ComponentLookup = state.GetComponentLookup<BicycleOwner>(true);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentLookup = state.GetComponentLookup<CurrentBuilding>(true);
				this.__Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup = state.GetComponentLookup<SpawnableBuildingData>(true);
				this.__Game_Prefabs_MailAccumulationData_RO_ComponentLookup = state.GetComponentLookup<MailAccumulationData>(true);
				this.__Game_Prefabs_ServiceObjectData_RO_ComponentLookup = state.GetComponentLookup<ServiceObjectData>(true);
				this.__Game_Citizens_MailSender_RW_ComponentLookup = state.GetComponentLookup<MailSender>(false);
				this.__Game_Buildings_MailProducer_RW_ComponentLookup = state.GetComponentLookup<MailProducer>(false);
				this.__Game_Buildings_CitizenPresence_RW_ComponentLookup = state.GetComponentLookup<CitizenPresence>(false);
			}

			// Token: 0x04009272 RID: 37490
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x04009273 RID: 37491
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x04009274 RID: 37492
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x04009275 RID: 37493
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			// Token: 0x04009276 RID: 37494
			public SharedComponentTypeHandle<UpdateFrame> __Game_Simulation_UpdateFrame_SharedComponentTypeHandle;

			// Token: 0x04009277 RID: 37495
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentTypeHandle;

			// Token: 0x04009278 RID: 37496
			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			// Token: 0x04009279 RID: 37497
			[ReadOnly]
			public ComponentTypeHandle<Leisure> __Game_Citizens_Leisure_RO_ComponentTypeHandle;

			// Token: 0x0400927A RID: 37498
			public ComponentLookup<HouseholdNeed> __Game_Citizens_HouseholdNeed_RW_ComponentLookup;

			// Token: 0x0400927B RID: 37499
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x0400927C RID: 37500
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x0400927D RID: 37501
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

			// Token: 0x0400927E RID: 37502
			[ReadOnly]
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentLookup;

			// Token: 0x0400927F RID: 37503
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RW_ComponentLookup;

			// Token: 0x04009280 RID: 37504
			[ReadOnly]
			public ComponentLookup<MovingAway> __Game_Agents_MovingAway_RO_ComponentLookup;

			// Token: 0x04009281 RID: 37505
			[ReadOnly]
			public ComponentLookup<Worker> __Game_Citizens_Worker_RO_ComponentLookup;

			// Token: 0x04009282 RID: 37506
			[ReadOnly]
			public ComponentLookup<Game.Citizens.Student> __Game_Citizens_Student_RO_ComponentLookup;

			// Token: 0x04009283 RID: 37507
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x04009284 RID: 37508
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

			// Token: 0x04009285 RID: 37509
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x04009286 RID: 37510
			[ReadOnly]
			public ComponentLookup<InDanger> __Game_Events_InDanger_RO_ComponentLookup;

			// Token: 0x04009287 RID: 37511
			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> __Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup;

			// Token: 0x04009288 RID: 37512
			public ComponentLookup<CoordinatedMeeting> __Game_Citizens_CoordinatedMeeting_RW_ComponentLookup;

			// Token: 0x04009289 RID: 37513
			[ReadOnly]
			public ComponentLookup<AttendingMeeting> __Game_Citizens_AttendingMeeting_RO_ComponentLookup;

			// Token: 0x0400928A RID: 37514
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> __Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup;

			// Token: 0x0400928B RID: 37515
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x0400928C RID: 37516
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> __Game_Buildings_Student_RO_BufferLookup;

			// Token: 0x0400928D RID: 37517
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;

			// Token: 0x0400928E RID: 37518
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> __Game_Prefabs_OutsideConnectionData_RO_ComponentLookup;

			// Token: 0x0400928F RID: 37519
			[ReadOnly]
			public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;

			// Token: 0x04009290 RID: 37520
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> __Game_Citizens_CommuterHousehold_RO_ComponentLookup;

			// Token: 0x04009291 RID: 37521
			[ReadOnly]
			public ComponentLookup<Criminal> __Game_Citizens_Criminal_RO_ComponentLookup;

			// Token: 0x04009292 RID: 37522
			[ReadOnly]
			public ComponentLookup<LeisureSeekerCooldown> __Game_Citizens_LeisureSeekerCooldown_RO_ComponentLookup;

			// Token: 0x04009293 RID: 37523
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RW_ComponentLookup;

			// Token: 0x04009294 RID: 37524
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x04009295 RID: 37525
			[ReadOnly]
			public BufferLookup<DistrictModifier> __Game_Areas_DistrictModifier_RO_BufferLookup;

			// Token: 0x04009296 RID: 37526
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> __Game_Areas_CurrentDistrict_RO_ComponentLookup;

			// Token: 0x04009297 RID: 37527
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x04009298 RID: 37528
			[ReadOnly]
			public ComponentLookup<BicycleOwner> __Game_Citizens_BicycleOwner_RO_ComponentLookup;

			// Token: 0x04009299 RID: 37529
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			// Token: 0x0400929A RID: 37530
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;

			// Token: 0x0400929B RID: 37531
			[ReadOnly]
			public ComponentLookup<MailAccumulationData> __Game_Prefabs_MailAccumulationData_RO_ComponentLookup;

			// Token: 0x0400929C RID: 37532
			[ReadOnly]
			public ComponentLookup<ServiceObjectData> __Game_Prefabs_ServiceObjectData_RO_ComponentLookup;

			// Token: 0x0400929D RID: 37533
			public ComponentLookup<MailSender> __Game_Citizens_MailSender_RW_ComponentLookup;

			// Token: 0x0400929E RID: 37534
			public ComponentLookup<MailProducer> __Game_Buildings_MailProducer_RW_ComponentLookup;

			// Token: 0x0400929F RID: 37535
			public ComponentLookup<CitizenPresence> __Game_Buildings_CitizenPresence_RW_ComponentLookup;
		}
	}
}
