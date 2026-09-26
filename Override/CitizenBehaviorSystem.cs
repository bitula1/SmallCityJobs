using System;
using System.Runtime.CompilerServices;
using Colossal.Collections;
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
	// Token: 0x02001520 RID: 5408
	public partial class CitizenBehaviorSystem : GameSystemBase
	{
		// Token: 0x06006888 RID: 26760 RVA: 0x0035D8E5 File Offset: 0x0035BAE5
		public override int GetUpdateInterval(SystemUpdatePhase phase)
		{
			return 16;
		}

		// Token: 0x06006889 RID: 26761 RVA: 0x000471D2 File Offset: 0x000453D2
		public override int GetUpdateOffset(SystemUpdatePhase phase)
		{
			return 11;
		}

		// Token: 0x0600688A RID: 26762 RVA: 0x0037DBE4 File Offset: 0x0037BDE4
		public static float2 GetSleepTime(Entity entity, Citizen citizen, ref EconomyParameterData economyParameters, bool isWorker, Worker worker, bool isStudent, Game.Citizens.Student student)
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
			if (isWorker)
			{
				float2 = WorkerSystem.GetTimeToWork(citizen, worker, ref economyParameters, true);
			}
			else
			{
				if (!isStudent)
				{
					return @float;
				}
				float2 = StudentSystem.GetTimeToStudy(citizen, student, ref economyParameters);
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

		// Token: 0x0600688B RID: 26763 RVA: 0x0037DD1C File Offset: 0x0037BF1C
		public static bool IsSleepTime(Entity entity, Citizen citizen, ref EconomyParameterData economyParameters, float normalizedTime, bool isWorker, Worker worker, bool isStudent, Game.Citizens.Student student)
		{
			float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(entity, citizen, ref economyParameters, isWorker, worker, isStudent, student);
			if (sleepTime.y < sleepTime.x)
			{
				return normalizedTime > sleepTime.x || normalizedTime < sleepTime.y;
			}
			return normalizedTime > sleepTime.x && normalizedTime < sleepTime.y;
		}

		// Token: 0x0600688C RID: 26764 RVA: 0x0037DD71 File Offset: 0x0037BF71
		public NativeQueue<Entity>.ParallelWriter GetCarReserveQueue(out JobHandle deps)
		{
			deps = this.m_CarReserveWriters;
			return this.m_ParallelCarReserveQueue;
		}

		// Token: 0x0600688D RID: 26765 RVA: 0x0037DD85 File Offset: 0x0037BF85
		public void AddCarReserveWriter(JobHandle writer)
		{
			this.m_CarReserveWriters = JobHandle.CombineDependencies(this.m_CarReserveWriters, writer);
		}

		// Token: 0x0600688E RID: 26766 RVA: 0x0037DD9C File Offset: 0x0037BF9C
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

		// Token: 0x0600688F RID: 26767 RVA: 0x0037DFEB File Offset: 0x0037C1EB
		[Preserve]
		protected override void OnDestroy()
		{
			this.m_CarReserveQueue.Dispose();
			base.OnDestroy();
		}

		// Token: 0x06006890 RID: 26768 RVA: 0x0037E000 File Offset: 0x0037C200
		[Preserve]
		protected override void OnUpdate()
		{
			uint updateFrameWithInterval = SimulationUtils.GetUpdateFrameWithInterval(this.m_SimulationSystem.frameIndex, (uint)this.GetUpdateInterval(SystemUpdatePhase.GameSimulation), 16);
			NativeQueue<Entity> nativeQueue = new NativeQueue<Entity>(Allocator.TempJob);
			NativeQueue<Entity> nativeQueue2 = new NativeQueue<Entity>(Allocator.TempJob);
			this.m_CitizenQuery.ResetFilter();
			this.m_CitizenQuery.AddSharedComponentFilter<UpdateFrame>(new UpdateFrame(updateFrameWithInterval));
			float num = 150f;
			ParkingParametersData parkingParametersData;
			if (this.__query_963917315_0.TryGetSingleton<ParkingParametersData>(out parkingParametersData) && parkingParametersData.m_MaxParkingSearchDistance > 0f)
			{
				num = parkingParametersData.m_MaxParkingSearchDistance;
			}
			CitizenBehaviorSystem.CitizenAITickJob citizenAITickJob = default(CitizenBehaviorSystem.CitizenAITickJob);
			citizenAITickJob.m_CitizenType = InternalCompilerInterface.GetComponentTypeHandle<Citizen>(ref this.__TypeHandle.__Game_Citizens_Citizen_RW_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_CurrentBuildingType = InternalCompilerInterface.GetComponentTypeHandle<CurrentBuilding>(ref this.__TypeHandle.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_EntityType = InternalCompilerInterface.GetEntityTypeHandle(ref this.__TypeHandle.__Unity_Entities_Entity_TypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdMemberType = InternalCompilerInterface.GetComponentTypeHandle<HouseholdMember>(ref this.__TypeHandle.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HealthProblemType = InternalCompilerInterface.GetComponentTypeHandle<HealthProblem>(ref this.__TypeHandle.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_TripType = InternalCompilerInterface.GetBufferTypeHandle<TripNeeded>(ref this.__TypeHandle.__Game_Citizens_TripNeeded_RW_BufferTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_LeisureType = InternalCompilerInterface.GetComponentTypeHandle<Leisure>(ref this.__TypeHandle.__Game_Citizens_Leisure_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_CriminalType = InternalCompilerInterface.GetComponentTypeHandle<Criminal>(ref this.__TypeHandle.__Game_Citizens_Criminal_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_WorkerType = InternalCompilerInterface.GetComponentTypeHandle<Worker>(ref this.__TypeHandle.__Game_Citizens_Worker_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_StudentType = InternalCompilerInterface.GetComponentTypeHandle<Game.Citizens.Student>(ref this.__TypeHandle.__Game_Citizens_Student_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_AttendingMeetingType = InternalCompilerInterface.GetComponentTypeHandle<AttendingMeeting>(ref this.__TypeHandle.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_CarKeeperType = InternalCompilerInterface.GetComponentTypeHandle<CarKeeper>(ref this.__TypeHandle.__Game_Citizens_CarKeeper_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_LeisureSeekerCooldownType = InternalCompilerInterface.GetComponentTypeHandle<LeisureSeekerCooldown>(ref this.__TypeHandle.__Game_Citizens_LeisureSeekerCooldown_RO_ComponentTypeHandle, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdNeeds = InternalCompilerInterface.GetComponentLookup<HouseholdNeed>(ref this.__TypeHandle.__Game_Citizens_HouseholdNeed_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Households = InternalCompilerInterface.GetComponentLookup<Household>(ref this.__TypeHandle.__Game_Citizens_Household_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_PropertyRenters = InternalCompilerInterface.GetComponentLookup<PropertyRenter>(ref this.__TypeHandle.__Game_Buildings_PropertyRenter_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Transforms = InternalCompilerInterface.GetComponentLookup<Game.Objects.Transform>(ref this.__TypeHandle.__Game_Objects_Transform_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_MaxParkingSearchDistance = num;
			citizenAITickJob.m_PersonalCars = InternalCompilerInterface.GetComponentLookup<Game.Vehicles.PersonalCar>(ref this.__TypeHandle.__Game_Vehicles_PersonalCar_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_ParkedCarData = InternalCompilerInterface.GetComponentLookup<ParkedCar>(ref this.__TypeHandle.__Game_Vehicles_ParkedCar_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_MovingAway = InternalCompilerInterface.GetComponentLookup<MovingAway>(ref this.__TypeHandle.__Game_Agents_MovingAway_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_TouristHouseholds = InternalCompilerInterface.GetComponentLookup<TouristHousehold>(ref this.__TypeHandle.__Game_Citizens_TouristHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_HomelessHouseholds = InternalCompilerInterface.GetComponentLookup<HomelessHousehold>(ref this.__TypeHandle.__Game_Citizens_HomelessHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OutsideConnections = InternalCompilerInterface.GetComponentLookup<Game.Objects.OutsideConnection>(ref this.__TypeHandle.__Game_Objects_OutsideConnection_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_InDangerData = InternalCompilerInterface.GetComponentLookup<InDanger>(ref this.__TypeHandle.__Game_Events_InDanger_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Attendees = InternalCompilerInterface.GetBufferLookup<CoordinatedMeetingAttendee>(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Meetings = InternalCompilerInterface.GetComponentLookup<CoordinatedMeeting>(ref this.__TypeHandle.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_MeetingDatas = InternalCompilerInterface.GetBufferLookup<HaveCoordinatedMeetingData>(ref this.__TypeHandle.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_Prefabs = InternalCompilerInterface.GetComponentLookup<PrefabRef>(ref this.__TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_BuildingStudents = InternalCompilerInterface.GetBufferLookup<Game.Buildings.Student>(ref this.__TypeHandle.__Game_Buildings_Student_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_PopulationData = InternalCompilerInterface.GetComponentLookup<Population>(ref this.__TypeHandle.__Game_City_Population_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OutsideConnectionDatas = InternalCompilerInterface.GetComponentLookup<OutsideConnectionData>(ref this.__TypeHandle.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_OwnedVehicles = InternalCompilerInterface.GetBufferLookup<OwnedVehicle>(ref this.__TypeHandle.__Game_Vehicles_OwnedVehicle_RO_BufferLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_CommuterHouseholds = InternalCompilerInterface.GetComponentLookup<CommuterHousehold>(ref this.__TypeHandle.__Game_Citizens_CommuterHousehold_RO_ComponentLookup, ref base.CheckedStateRef);
			citizenAITickJob.m_HouseholdArchetype = this.m_HouseholdArchetype;
			JobHandle jobHandle;
			citizenAITickJob.m_OutsideConnectionEntities = this.m_OutsideConnectionQuery.ToEntityListAsync(Allocator.TempJob, out jobHandle);
			citizenAITickJob.m_EconomyParameters = this.m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();
			citizenAITickJob.m_LeisureParameters = this.m_LeisureParameterQuery.GetSingleton<LeisureParametersData>();
			citizenAITickJob.m_CommandBuffer = this.m_EndFrameBarrier.CreateCommandBuffer().AsParallelWriter();
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

		// Token: 0x06006891 RID: 26769 RVA: 0x0037E83C File Offset: 0x0037CA3C
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void __AssignQueries(ref SystemState state)
		{
			EntityQueryBuilder entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp);
			this.__query_963917315_0 = entityQueryBuilder.WithAll<ParkingParametersData>().WithOptions(EntityQueryOptions.IncludeSystems).Build(ref state);
			entityQueryBuilder.Reset();
			entityQueryBuilder.Dispose();
		}

		// Token: 0x06006892 RID: 26770 RVA: 0x0037E885 File Offset: 0x0037CA85
		protected override void OnCreateForCompiler()
		{
			base.OnCreateForCompiler();
			this.__AssignQueries(ref base.CheckedStateRef);
			this.__TypeHandle.__AssignHandles(ref base.CheckedStateRef);
		}

		// Token: 0x06006893 RID: 26771 RVA: 0x00006FBB File Offset: 0x000051BB
		[Preserve]
		public CitizenBehaviorSystem()
		{
		}

		// Token: 0x04009381 RID: 37761
		public static readonly float kMaxPathfindCost = 17000f;

		// Token: 0x04009382 RID: 37762
		public static readonly int kMinLeisurePossibility = 80;

		// Token: 0x04009383 RID: 37763
		public static readonly uint kLeisureSeekerCooldownFrames = 20000U;

		// Token: 0x04009384 RID: 37764
		private JobHandle m_CarReserveWriters;

		// Token: 0x04009385 RID: 37765
		private EntityQuery m_CitizenQuery;

		// Token: 0x04009386 RID: 37766
		private EntityQuery m_OutsideConnectionQuery;

		// Token: 0x04009387 RID: 37767
		private EntityQuery m_EconomyParameterQuery;

		// Token: 0x04009388 RID: 37768
		private EntityQuery m_LeisureParameterQuery;

		// Token: 0x04009389 RID: 37769
		private EntityQuery m_TimeDataQuery;

		// Token: 0x0400938A RID: 37770
		private EntityQuery m_PopulationQuery;

		// Token: 0x0400938B RID: 37771
		private SimulationSystem m_SimulationSystem;

		// Token: 0x0400938C RID: 37772
		private TimeSystem m_TimeSystem;

		// Token: 0x0400938D RID: 37773
		private EndFrameBarrier m_EndFrameBarrier;

		// Token: 0x0400938E RID: 37774
		private EntityArchetype m_HouseholdArchetype;

		// Token: 0x0400938F RID: 37775
		private NativeQueue<Entity> m_CarReserveQueue;

		// Token: 0x04009390 RID: 37776
		private NativeQueue<Entity>.ParallelWriter m_ParallelCarReserveQueue;

		// Token: 0x04009391 RID: 37777
		private CitizenBehaviorSystem.TypeHandle __TypeHandle;

		// Token: 0x04009392 RID: 37778
		private EntityQuery __query_963917315_0;

		// Token: 0x02001521 RID: 5409
		[BurstCompile]
		private struct CitizenReserveHouseholdCarJob : IJob
		{
			// Token: 0x06006895 RID: 26773 RVA: 0x0037E8C8 File Offset: 0x0037CAC8
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

			// Token: 0x04009393 RID: 37779
			public ComponentLookup<CarKeeper> m_CarKeepers;

			// Token: 0x04009394 RID: 37780
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCars;

			// Token: 0x04009395 RID: 37781
			[ReadOnly]
			public ComponentLookup<HouseholdMember> m_HouseholdMembers;

			// Token: 0x04009396 RID: 37782
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x04009397 RID: 37783
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x04009398 RID: 37784
			[ReadOnly]
			public BufferLookup<DistrictModifier> m_DistrictModifiers;

			// Token: 0x04009399 RID: 37785
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> m_CurrentDistricts;

			// Token: 0x0400939A RID: 37786
			[ReadOnly]
			public ComponentLookup<Citizen> m_Citizens;

			// Token: 0x0400939B RID: 37787
			[ReadOnly]
			public ComponentLookup<BicycleOwner> m_BicycleOwners;

			// Token: 0x0400939C RID: 37788
			public NativeQueue<Entity> m_ReserverQueue;
		}

		// Token: 0x02001522 RID: 5410
		[BurstCompile]
		private struct CitizenTryCollectMailJob : IJob
		{
			// Token: 0x06006896 RID: 26774 RVA: 0x0037EA40 File Offset: 0x0037CC40
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

			// Token: 0x06006897 RID: 26775 RVA: 0x0037EB50 File Offset: 0x0037CD50
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

			// Token: 0x0400939D RID: 37789
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			// Token: 0x0400939E RID: 37790
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_PrefabRefData;

			// Token: 0x0400939F RID: 37791
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> m_SpawnableBuildingData;

			// Token: 0x040093A0 RID: 37792
			[ReadOnly]
			public ComponentLookup<MailAccumulationData> m_MailAccumulationData;

			// Token: 0x040093A1 RID: 37793
			[ReadOnly]
			public ComponentLookup<ServiceObjectData> m_ServiceObjectData;

			// Token: 0x040093A2 RID: 37794
			public ComponentLookup<MailSender> m_MailSenderData;

			// Token: 0x040093A3 RID: 37795
			public ComponentLookup<MailProducer> m_MailProducerData;

			// Token: 0x040093A4 RID: 37796
			public NativeQueue<Entity> m_MailSenderQueue;
		}

		// Token: 0x02001523 RID: 5411
		[BurstCompile]
		private struct CitizeSleepJob : IJob
		{
			// Token: 0x06006898 RID: 26776 RVA: 0x0037EBE8 File Offset: 0x0037CDE8
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

			// Token: 0x040093A5 RID: 37797
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> m_CurrentBuildingData;

			// Token: 0x040093A6 RID: 37798
			public ComponentLookup<CitizenPresence> m_CitizenPresenceData;

			// Token: 0x040093A7 RID: 37799
			public NativeQueue<Entity> m_SleepQueue;
		}

		// Token: 0x02001524 RID: 5412
		[BurstCompile]
		private struct CitizenAITickJob : IJobChunk
		{
			// Token: 0x06006899 RID: 26777 RVA: 0x0037EC70 File Offset: 0x0037CE70
			private bool CheckSleep(int index, Entity entity, ref Citizen citizen, Entity currentBuilding, Entity home, DynamicBuffer<TripNeeded> trips, ref EconomyParameterData economyParameters, bool isWorker, Worker worker, bool isStudent, Game.Citizens.Student student, bool isCarKeeper, Entity carEntity)
			{
				if (home != Entity.Null && CitizenBehaviorSystem.IsSleepTime(entity, citizen, ref economyParameters, this.m_NormalizedTime, isWorker, worker, isStudent, student))
				{
					if (currentBuilding == home)
					{
						this.m_CommandBuffer.AddComponent<TravelPurpose>(index, entity, new TravelPurpose
						{
							m_Purpose = Purpose.Sleeping
						});
						this.m_SleepQueue.Enqueue(entity);
						this.ReleaseCar(index, entity, isCarKeeper, carEntity, home);
					}
					else
					{
						this.GoHome(entity, home, trips, currentBuilding, isCarKeeper);
					}
					return true;
				}
				return false;
			}

			// Token: 0x0600689A RID: 26778 RVA: 0x0037ED00 File Offset: 0x0037CF00
			private void GoHome(Entity entity, Entity target, DynamicBuffer<TripNeeded> trips, Entity currentBuilding, bool isCarKeeper)
			{
				if (target == Entity.Null)
				{
					return;
				}
				if (currentBuilding == target)
				{
					return;
				}
				if (!isCarKeeper)
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

			// Token: 0x0600689B RID: 26779 RVA: 0x0037ED74 File Offset: 0x0037CF74
			private void GoToOutsideConnection(Entity entity, Entity household, Entity currentBuilding, Entity targetBuilding, ref Citizen citizen, DynamicBuffer<TripNeeded> trips, Purpose purpose, ref Unity.Mathematics.Random random, bool isCarKeeper)
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
					if (!isCarKeeper)
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

			// Token: 0x0600689C RID: 26780 RVA: 0x0037EEB4 File Offset: 0x0037D0B4
			private void GoShopping(int chunkIndex, Entity citizen, bool isCarKeeper, Entity household, HouseholdNeed need, float3 position)
			{
				if (!isCarKeeper)
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

			// Token: 0x0600689D RID: 26781 RVA: 0x0037EF28 File Offset: 0x0037D128
			private float GetTimeLeftUntilInterval(float2 interval)
			{
				if (this.m_NormalizedTime >= interval.x)
				{
					return 1f - this.m_NormalizedTime + interval.x;
				}
				return interval.x - this.m_NormalizedTime;
			}

			// Token: 0x0600689E RID: 26782 RVA: 0x0037EF5C File Offset: 0x0037D15C
			private bool DoLeisure(int chunkIndex, int citizenIndex, Entity citizenEntity, Entity householdEntity, Entity currentBuilding, Entity homeEntity, bool isTourist, ref Citizen citizenData, int population, ref Unity.Mathematics.Random random, ref EconomyParameterData economyParameters, bool isWorker, Worker worker, bool isStudent, Game.Citizens.Student student, ref NativeArray<LeisureSeekerCooldown> leisureSeekerCooldown)
			{
				bool scjLeisure = m_SmallCityJobs.DoLeisure(citizenEntity, this.m_SimulationFrame);

				bool flag = homeEntity == Entity.Null && CitizenUtils.HasMovedIn(householdEntity, this.m_Households);
				if (isTourist)
				{
					if (this.m_OutsideConnections.HasComponent(currentBuilding) && this.m_TouristHouseholds[householdEntity].m_Hotel != Entity.Null)
					{
						return false;
					}
				}
				else if (!flag)
				{
					LeisureSeekerCooldown leisureSeekerCooldown2;
					if (!scjLeisure && CollectionUtils.TryGet<LeisureSeekerCooldown>(leisureSeekerCooldown, citizenIndex, out leisureSeekerCooldown2) && this.m_SimulationFrame < leisureSeekerCooldown2.m_SimulationFrame + CitizenBehaviorSystem.kLeisureSeekerCooldownFrames)
					{
						return false;
					}
					int num = (int)(128 - citizenData.m_LeisureCounter);
					if (this.m_OutsideConnections.HasComponent(currentBuilding) || (!scjLeisure && random.NextInt(this.m_LeisureParameters.m_LeisureRandomFactor) > num))
					{
						return false;
					}
				}
				int num2 = math.min(CitizenBehaviorSystem.kMinLeisurePossibility, Mathf.RoundToInt(200f / math.max(1f, math.sqrt(economyParameters.m_TrafficReduction * (float)population))));
                if (!isTourist && !flag && !scjLeisure && random.NextInt(100) > num2) {
					citizenData.m_LeisureCounter = byte.MaxValue;
					return true;
				}
				float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(citizenEntity, citizenData, ref economyParameters, isWorker, worker, isStudent, student);
				float num3 = this.GetTimeLeftUntilInterval(sleepTime);
				if (isWorker)
				{
					citizenData.m_UnemploymentTimeCounter = 0f;
					float2 timeToWork = WorkerSystem.GetTimeToWork(citizenData, worker, ref economyParameters, true);
					num3 = math.min(num3, this.GetTimeLeftUntilInterval(timeToWork));
				}
				else if (isStudent)
				{
					citizenData.m_UnemploymentTimeCounter = 0f;
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

			// Token: 0x0600689F RID: 26783 RVA: 0x0037F138 File Offset: 0x0037D338
			private void ReleaseCar(int chunkIndex, Entity citizen, bool isCarKeeper, Entity car, Entity home)
			{
				if (isCarKeeper)
				{
					if (this.m_PersonalCars.HasComponent(car))
					{
						Game.Vehicles.PersonalCar personalCar = this.m_PersonalCars[car];
						personalCar.m_Keeper = Entity.Null;
						this.m_PersonalCars[car] = personalCar;
						Game.Objects.Transform transform;
						Game.Objects.Transform transform2;
						if (home != Entity.Null && (personalCar.m_State & PersonalCarFlags.HomeTarget) == (PersonalCarFlags)0U && this.m_ParkedCarData.HasComponent(car) && this.m_Transforms.TryGetComponent(car, out transform) && this.m_Transforms.TryGetComponent(home, out transform2) && math.distancesq(transform.m_Position, transform2.m_Position) > this.m_MaxParkingSearchDistance * this.m_MaxParkingSearchDistance)
						{
							this.m_CommandBuffer.AddComponent<FixParkingLocation>(chunkIndex, car, new FixParkingLocation(Entity.Null, home));
							this.m_CommandBuffer.AddComponent<Updated>(chunkIndex, car);
						}
					}
					this.m_CommandBuffer.SetComponentEnabled<CarKeeper>(chunkIndex, citizen, false);
				}
			}

			// Token: 0x060068A0 RID: 26784 RVA: 0x0037F228 File Offset: 0x0037D428
			private bool AttendMeeting(int chunkIndex, Entity entity, ref Citizen citizen, AttendingMeeting attendingMeeting, bool isCarKeeper, Entity household, Entity currentBuilding, DynamicBuffer<TripNeeded> trips, ref Unity.Mathematics.Random random)
			{
				if (!isCarKeeper)
				{
					this.m_CarReserverQueue.Enqueue(entity);
				}
				Entity meeting = attendingMeeting.m_Meeting;
				if (this.m_Attendees.HasBuffer(meeting) && this.m_Meetings.HasComponent(meeting))
				{
					CoordinatedMeeting coordinatedMeeting = this.m_Meetings[meeting];
					if (coordinatedMeeting.m_Status != MeetingStatus.Done && this.m_Prefabs.HasComponent(meeting))
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
									this.GoShopping(chunkIndex, entity, isCarKeeper, household, new HouseholdNeed
									{
										m_Resource = haveCoordinatedMeetingData.m_TravelPurpose.m_Resource,
										m_Amount = haveCoordinatedMeetingData.m_TravelPurpose.m_Data
									}, position);
									return true;
								}
								if (haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose == Purpose.Traveling)
								{
									Citizen citizen2 = default(Citizen);
									this.GoToOutsideConnection(entity, household, currentBuilding, Entity.Null, ref citizen2, trips, haveCoordinatedMeetingData.m_TravelPurpose.m_Purpose, ref random, isCarKeeper);
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
									Entity entity2 = Entity.Null;
									if (this.m_PropertyRenters.HasComponent(household))
									{
										entity2 = this.m_PropertyRenters[household].m_Property;
									}
									else if (this.m_HomelessHouseholds.HasComponent(household))
									{
										entity2 = this.m_HomelessHouseholds[household].m_TempHome;
									}
									if (entity2 != Entity.Null)
									{
										coordinatedMeeting.m_Target = entity2;
										this.m_Meetings[meeting] = coordinatedMeeting;
										this.GoHome(entity, entity2, trips, currentBuilding, isCarKeeper);
									}
									else
									{
										coordinatedMeeting.m_Status = MeetingStatus.Done;
										this.m_Meetings[meeting] = coordinatedMeeting;
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

			// Token: 0x060068A1 RID: 26785 RVA: 0x0037F5FC File Offset: 0x0037D7FC
			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				Unity.Mathematics.Random random = this.m_RandomSeed.GetRandom(unfilteredChunkIndex);
				NativeArray<Entity> nativeArray = chunk.GetNativeArray(this.m_EntityType);
				NativeArray<Citizen> nativeArray2 = chunk.GetNativeArray<Citizen>(ref this.m_CitizenType);
				NativeArray<HouseholdMember> nativeArray3 = chunk.GetNativeArray<HouseholdMember>(ref this.m_HouseholdMemberType);
				NativeArray<CurrentBuilding> nativeArray4 = chunk.GetNativeArray<CurrentBuilding>(ref this.m_CurrentBuildingType);
				NativeArray<HealthProblem> nativeArray5 = chunk.GetNativeArray<HealthProblem>(ref this.m_HealthProblemType);
				BufferAccessor<TripNeeded> bufferAccessor = chunk.GetBufferAccessor<TripNeeded>(ref this.m_TripType);
				NativeArray<Criminal> nativeArray6 = chunk.GetNativeArray<Criminal>(ref this.m_CriminalType);
				NativeArray<Worker> nativeArray7 = chunk.GetNativeArray<Worker>(ref this.m_WorkerType);
				NativeArray<Game.Citizens.Student> nativeArray8 = chunk.GetNativeArray<Game.Citizens.Student>(ref this.m_StudentType);
				NativeArray<AttendingMeeting> nativeArray9 = chunk.GetNativeArray<AttendingMeeting>(ref this.m_AttendingMeetingType);
				NativeArray<LeisureSeekerCooldown> nativeArray10 = chunk.GetNativeArray<LeisureSeekerCooldown>(ref this.m_LeisureSeekerCooldownType);
				NativeArray<CarKeeper> nativeArray11 = chunk.GetNativeArray<CarKeeper>(ref this.m_CarKeeperType);
				EnabledMask enabledMask = chunk.GetEnabledMask<CarKeeper>(ref this.m_CarKeeperType);
				int population = this.m_PopulationData[this.m_PopulationEntity].m_Population;
				bool flag = chunk.Has<HealthProblem>(ref this.m_HealthProblemType);
				bool flag2 = chunk.Has<Leisure>(ref this.m_LeisureType);
				bool flag3 = chunk.Has<Worker>(ref this.m_WorkerType);
				bool flag4 = chunk.Has<Game.Citizens.Student>(ref this.m_StudentType);
				bool flag5 = chunk.Has<CarKeeper>(ref this.m_CarKeeperType);
				for (int i = 0; i < nativeArray.Length; i++)
				{
					Citizen citizen = nativeArray2[i];
					if (!flag || !CitizenUtils.IsDead(nativeArray5[i]))
					{
						Entity entity = nativeArray3[i].m_Household;
						Entity entity2 = nativeArray[i];
                        m_SmallCityJobs.init(entity2, SmallCityJobsPhase.StartLeisure);
                        bool flag6 = (citizen.m_State & CitizenFlags.Tourist) > CitizenFlags.None;
						Criminal criminal;
						if (!CollectionUtils.TryGet<Criminal>(nativeArray6, i, out criminal) || (criminal.m_Flags & (CriminalFlags.Prisoner | CriminalFlags.Arrested | CriminalFlags.Sentenced)) == (CriminalFlags)0)
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
									bool flag7 = (citizen.m_State & CitizenFlags.Commuter) > CitizenFlags.None;
									CitizenAge age = citizen.GetAge();
									if (flag7 && (age == CitizenAge.Elderly || age == CitizenAge.Child))
									{
										this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
									}
									bool flag8 = flag5 && enabledMask[i];
									MovingAway movingAway;
									if ((citizen.m_State & CitizenFlags.MovingAwayReachOC) != CitizenFlags.None)
									{
										this.m_CommandBuffer.AddComponent<Deleted>(unfilteredChunkIndex, entity2, default(Deleted));
									}
									else if (this.m_MovingAway.TryGetComponent(entity, out movingAway))
									{
										this.GoToOutsideConnection(entity2, entity, currentBuilding, movingAway.m_Target, ref citizen, dynamicBuffer, Purpose.MovingAway, ref random, flag8);
										if (flag2)
										{
											this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
										}
										if (flag3)
										{
											this.m_CommandBuffer.RemoveComponent<Worker>(unfilteredChunkIndex, entity2);
										}
										if (flag4)
										{
											Entity school = nativeArray8[i].m_School;
											if (this.m_BuildingStudents.HasBuffer(school))
											{
												this.m_CommandBuffer.AddComponent<StudentsRemoved>(unfilteredChunkIndex, school);
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
										else if (flag6)
										{
											Entity hotel = this.m_TouristHouseholds[entity].m_Hotel;
											if (this.m_PropertyRenters.HasComponent(hotel))
											{
												entity3 = this.m_PropertyRenters[hotel].m_Property;
											}
										}
										else if (flag7)
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
										else if (this.m_HomelessHouseholds.HasComponent(entity))
										{
											entity3 = this.m_HomelessHouseholds[entity].m_TempHome;
										}
										AttendingMeeting attendingMeeting;
										if (flag)
										{
											if (flag2)
											{
												this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
											}
										}
										else if (!CollectionUtils.TryGet<AttendingMeeting>(nativeArray9, i, out attendingMeeting) || !this.AttendMeeting(unfilteredChunkIndex, entity2, ref citizen, attendingMeeting, flag8, entity, currentBuilding, dynamicBuffer, ref random))
										{
											Worker worker = (flag3 ? nativeArray7[i] : default(Worker));
											Game.Citizens.Student student = (flag4 ? nativeArray8[i] : default(Game.Citizens.Student));
											if ((flag3 && !WorkerSystem.IsTodayOffDay(citizen, ref this.m_EconomyParameters, this.m_SimulationFrame, this.m_TimeData, population) && WorkerSystem.IsTimeToWork(citizen, worker, ref this.m_EconomyParameters, this.m_NormalizedTime)) || (flag4 && StudentSystem.IsTimeToStudy(citizen, student, ref this.m_EconomyParameters, this.m_NormalizedTime, this.m_SimulationFrame, this.m_TimeData, population)))
											{
												if (flag2)
												{
													this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
												}
											}
											else
											{
												Entity entity4 = (flag5 ? nativeArray11[i].m_Car : default(Entity));
												if (this.CheckSleep(i, entity2, ref citizen, currentBuilding, entity3, dynamicBuffer, ref this.m_EconomyParameters, flag3, worker, flag4, student, flag8, entity4))
												{
													if (flag2)
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
															this.GoShopping(unfilteredChunkIndex, entity2, flag8, entity, householdNeed, this.m_Transforms[currentBuilding].m_Position);
															householdNeed.m_Resource = Resource.NoResource;
															this.m_HouseholdNeeds[entity] = householdNeed;
															if (flag2)
															{
																this.m_CommandBuffer.RemoveComponent<Leisure>(unfilteredChunkIndex, entity2);
																goto IL_0733;
															}
															goto IL_0733;
														}
													}
													if (!flag2 && this.DoLeisure(unfilteredChunkIndex, i, entity2, entity, currentBuilding, entity3, flag6, ref citizen, population, ref random, ref this.m_EconomyParameters, flag3, worker, flag4, student, ref nativeArray10))
													{
														nativeArray2[i] = citizen;
													}
													else if (!flag2)
													{
														if (currentBuilding != entity3)
														{
															this.GoHome(entity2, entity3, dynamicBuffer, currentBuilding, flag8);
														}
														else
														{
															this.ReleaseCar(unfilteredChunkIndex, entity2, flag8, entity4, entity3);
														}
													}
												}
											}
										}
									}
								}
							}
						}
					}
					IL_0733:;
				}
			}

			// Token: 0x060068A2 RID: 26786 RVA: 0x0037FD50 File Offset: 0x0037DF50
			void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				this.Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
			}

			// Token: 0x040093A8 RID: 37800
			[ReadOnly]
			public EntityTypeHandle m_EntityType;

			// Token: 0x040093A9 RID: 37801
			public ComponentTypeHandle<Citizen> m_CitizenType;

			// Token: 0x040093AA RID: 37802
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> m_HouseholdMemberType;

			// Token: 0x040093AB RID: 37803
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> m_CurrentBuildingType;

			// Token: 0x040093AC RID: 37804
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> m_HealthProblemType;

			// Token: 0x040093AD RID: 37805
			public BufferTypeHandle<TripNeeded> m_TripType;

			// Token: 0x040093AE RID: 37806
			[ReadOnly]
			public ComponentTypeHandle<Leisure> m_LeisureType;

			// Token: 0x040093AF RID: 37807
			[ReadOnly]
			public ComponentTypeHandle<Criminal> m_CriminalType;

			// Token: 0x040093B0 RID: 37808
			[ReadOnly]
			public ComponentTypeHandle<Worker> m_WorkerType;

			// Token: 0x040093B1 RID: 37809
			[ReadOnly]
			public ComponentTypeHandle<Game.Citizens.Student> m_StudentType;

			// Token: 0x040093B2 RID: 37810
			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> m_AttendingMeetingType;

			// Token: 0x040093B3 RID: 37811
			[ReadOnly]
			public ComponentTypeHandle<CarKeeper> m_CarKeeperType;

			// Token: 0x040093B4 RID: 37812
			[ReadOnly]
			public ComponentTypeHandle<LeisureSeekerCooldown> m_LeisureSeekerCooldownType;

			// Token: 0x040093B5 RID: 37813
			[NativeDisableParallelForRestriction]
			public ComponentLookup<HouseholdNeed> m_HouseholdNeeds;

			// Token: 0x040093B6 RID: 37814
			[ReadOnly]
			public ComponentLookup<Household> m_Households;

			// Token: 0x040093B7 RID: 37815
			[ReadOnly]
			public ComponentLookup<PropertyRenter> m_PropertyRenters;

			// Token: 0x040093B8 RID: 37816
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> m_Transforms;

			// Token: 0x040093B9 RID: 37817
			public float m_MaxParkingSearchDistance;

			// Token: 0x040093BA RID: 37818
			[NativeDisableParallelForRestriction]
			public ComponentLookup<Game.Vehicles.PersonalCar> m_PersonalCars;

			// Token: 0x040093BB RID: 37819
			[ReadOnly]
			public ComponentLookup<ParkedCar> m_ParkedCarData;

			// Token: 0x040093BC RID: 37820
			[ReadOnly]
			public ComponentLookup<MovingAway> m_MovingAway;

			// Token: 0x040093BD RID: 37821
			[ReadOnly]
			public ComponentLookup<TouristHousehold> m_TouristHouseholds;

			// Token: 0x040093BE RID: 37822
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> m_HomelessHouseholds;

			// Token: 0x040093BF RID: 37823
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;

			// Token: 0x040093C0 RID: 37824
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> m_OutsideConnectionDatas;

			// Token: 0x040093C1 RID: 37825
			[ReadOnly]
			public ComponentLookup<InDanger> m_InDangerData;

			// Token: 0x040093C2 RID: 37826
			[NativeDisableParallelForRestriction]
			public ComponentLookup<CoordinatedMeeting> m_Meetings;

			// Token: 0x040093C3 RID: 37827
			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> m_Attendees;

			// Token: 0x040093C4 RID: 37828
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> m_MeetingDatas;

			// Token: 0x040093C5 RID: 37829
			[ReadOnly]
			public ComponentLookup<PrefabRef> m_Prefabs;

			// Token: 0x040093C6 RID: 37830
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> m_BuildingStudents;

			// Token: 0x040093C7 RID: 37831
			[ReadOnly]
			public ComponentLookup<Population> m_PopulationData;

			// Token: 0x040093C8 RID: 37832
			[ReadOnly]
			public BufferLookup<OwnedVehicle> m_OwnedVehicles;

			// Token: 0x040093C9 RID: 37833
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> m_CommuterHouseholds;

			// Token: 0x040093CA RID: 37834
			[ReadOnly]
			public EntityArchetype m_HouseholdArchetype;

			// Token: 0x040093CB RID: 37835
			[ReadOnly]
			public NativeList<Entity> m_OutsideConnectionEntities;

			// Token: 0x040093CC RID: 37836
			[ReadOnly]
			public EconomyParameterData m_EconomyParameters;

			// Token: 0x040093CD RID: 37837
			[ReadOnly]
			public LeisureParametersData m_LeisureParameters;

			// Token: 0x040093CE RID: 37838
			public float m_NormalizedTime;

			// Token: 0x040093CF RID: 37839
			public uint m_SimulationFrame;

			// Token: 0x040093D0 RID: 37840
			public EntityCommandBuffer.ParallelWriter m_CommandBuffer;

			// Token: 0x040093D1 RID: 37841
			public NativeQueue<Entity>.ParallelWriter m_CarReserverQueue;

			// Token: 0x040093D2 RID: 37842
			public NativeQueue<Entity>.ParallelWriter m_MailSenderQueue;

			// Token: 0x040093D3 RID: 37843
			public NativeQueue<Entity>.ParallelWriter m_SleepQueue;

			// Token: 0x040093D4 RID: 37844
			public TimeData m_TimeData;

			// Token: 0x040093D5 RID: 37845
			public Entity m_PopulationEntity;

			// Token: 0x040093D6 RID: 37846
			public RandomSeed m_RandomSeed;

            [ReadOnly]
            public SmallCityJobs m_SmallCityJobs;
        }

		// Token: 0x02001525 RID: 5413
		private struct TypeHandle
		{
			// Token: 0x060068A3 RID: 26787 RVA: 0x0037FD60 File Offset: 0x0037DF60
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void __AssignHandles(ref SystemState state)
			{
				this.__Game_Citizens_Citizen_RW_ComponentTypeHandle = state.GetComponentTypeHandle<Citizen>(false);
				this.__Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CurrentBuilding>(true);
				this.__Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
				this.__Game_Citizens_HouseholdMember_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HouseholdMember>(true);
				this.__Game_Citizens_HealthProblem_RO_ComponentTypeHandle = state.GetComponentTypeHandle<HealthProblem>(true);
				this.__Game_Citizens_TripNeeded_RW_BufferTypeHandle = state.GetBufferTypeHandle<TripNeeded>(false);
				this.__Game_Citizens_Leisure_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Leisure>(true);
				this.__Game_Citizens_Criminal_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Criminal>(true);
				this.__Game_Citizens_Worker_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Worker>(true);
				this.__Game_Citizens_Student_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Game.Citizens.Student>(true);
				this.__Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle = state.GetComponentTypeHandle<AttendingMeeting>(true);
				this.__Game_Citizens_CarKeeper_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CarKeeper>(true);
				this.__Game_Citizens_LeisureSeekerCooldown_RO_ComponentTypeHandle = state.GetComponentTypeHandle<LeisureSeekerCooldown>(true);
				this.__Game_Citizens_HouseholdNeed_RW_ComponentLookup = state.GetComponentLookup<HouseholdNeed>(false);
				this.__Game_Citizens_Household_RO_ComponentLookup = state.GetComponentLookup<Household>(true);
				this.__Game_Buildings_PropertyRenter_RO_ComponentLookup = state.GetComponentLookup<PropertyRenter>(true);
				this.__Game_Objects_Transform_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.Transform>(true);
				this.__Game_Vehicles_PersonalCar_RW_ComponentLookup = state.GetComponentLookup<Game.Vehicles.PersonalCar>(false);
				this.__Game_Vehicles_ParkedCar_RO_ComponentLookup = state.GetComponentLookup<ParkedCar>(true);
				this.__Game_Agents_MovingAway_RO_ComponentLookup = state.GetComponentLookup<MovingAway>(true);
				this.__Game_Citizens_TouristHousehold_RO_ComponentLookup = state.GetComponentLookup<TouristHousehold>(true);
				this.__Game_Citizens_HomelessHousehold_RO_ComponentLookup = state.GetComponentLookup<HomelessHousehold>(true);
				this.__Game_Objects_OutsideConnection_RO_ComponentLookup = state.GetComponentLookup<Game.Objects.OutsideConnection>(true);
				this.__Game_Events_InDanger_RO_ComponentLookup = state.GetComponentLookup<InDanger>(true);
				this.__Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup = state.GetBufferLookup<CoordinatedMeetingAttendee>(true);
				this.__Game_Citizens_CoordinatedMeeting_RW_ComponentLookup = state.GetComponentLookup<CoordinatedMeeting>(false);
				this.__Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup = state.GetBufferLookup<HaveCoordinatedMeetingData>(true);
				this.__Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(true);
				this.__Game_Buildings_Student_RO_BufferLookup = state.GetBufferLookup<Game.Buildings.Student>(true);
				this.__Game_City_Population_RO_ComponentLookup = state.GetComponentLookup<Population>(true);
				this.__Game_Prefabs_OutsideConnectionData_RO_ComponentLookup = state.GetComponentLookup<OutsideConnectionData>(true);
				this.__Game_Vehicles_OwnedVehicle_RO_BufferLookup = state.GetBufferLookup<OwnedVehicle>(true);
				this.__Game_Citizens_CommuterHousehold_RO_ComponentLookup = state.GetComponentLookup<CommuterHousehold>(true);
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

			// Token: 0x040093D7 RID: 37847
			public ComponentTypeHandle<Citizen> __Game_Citizens_Citizen_RW_ComponentTypeHandle;

			// Token: 0x040093D8 RID: 37848
			[ReadOnly]
			public ComponentTypeHandle<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentTypeHandle;

			// Token: 0x040093D9 RID: 37849
			[ReadOnly]
			public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

			// Token: 0x040093DA RID: 37850
			[ReadOnly]
			public ComponentTypeHandle<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentTypeHandle;

			// Token: 0x040093DB RID: 37851
			[ReadOnly]
			public ComponentTypeHandle<HealthProblem> __Game_Citizens_HealthProblem_RO_ComponentTypeHandle;

			// Token: 0x040093DC RID: 37852
			public BufferTypeHandle<TripNeeded> __Game_Citizens_TripNeeded_RW_BufferTypeHandle;

			// Token: 0x040093DD RID: 37853
			[ReadOnly]
			public ComponentTypeHandle<Leisure> __Game_Citizens_Leisure_RO_ComponentTypeHandle;

			// Token: 0x040093DE RID: 37854
			[ReadOnly]
			public ComponentTypeHandle<Criminal> __Game_Citizens_Criminal_RO_ComponentTypeHandle;

			// Token: 0x040093DF RID: 37855
			[ReadOnly]
			public ComponentTypeHandle<Worker> __Game_Citizens_Worker_RO_ComponentTypeHandle;

			// Token: 0x040093E0 RID: 37856
			[ReadOnly]
			public ComponentTypeHandle<Game.Citizens.Student> __Game_Citizens_Student_RO_ComponentTypeHandle;

			// Token: 0x040093E1 RID: 37857
			[ReadOnly]
			public ComponentTypeHandle<AttendingMeeting> __Game_Citizens_AttendingMeeting_RO_ComponentTypeHandle;

			// Token: 0x040093E2 RID: 37858
			[ReadOnly]
			public ComponentTypeHandle<CarKeeper> __Game_Citizens_CarKeeper_RO_ComponentTypeHandle;

			// Token: 0x040093E3 RID: 37859
			[ReadOnly]
			public ComponentTypeHandle<LeisureSeekerCooldown> __Game_Citizens_LeisureSeekerCooldown_RO_ComponentTypeHandle;

			// Token: 0x040093E4 RID: 37860
			public ComponentLookup<HouseholdNeed> __Game_Citizens_HouseholdNeed_RW_ComponentLookup;

			// Token: 0x040093E5 RID: 37861
			[ReadOnly]
			public ComponentLookup<Household> __Game_Citizens_Household_RO_ComponentLookup;

			// Token: 0x040093E6 RID: 37862
			[ReadOnly]
			public ComponentLookup<PropertyRenter> __Game_Buildings_PropertyRenter_RO_ComponentLookup;

			// Token: 0x040093E7 RID: 37863
			[ReadOnly]
			public ComponentLookup<Game.Objects.Transform> __Game_Objects_Transform_RO_ComponentLookup;

			// Token: 0x040093E8 RID: 37864
			public ComponentLookup<Game.Vehicles.PersonalCar> __Game_Vehicles_PersonalCar_RW_ComponentLookup;

			// Token: 0x040093E9 RID: 37865
			[ReadOnly]
			public ComponentLookup<ParkedCar> __Game_Vehicles_ParkedCar_RO_ComponentLookup;

			// Token: 0x040093EA RID: 37866
			[ReadOnly]
			public ComponentLookup<MovingAway> __Game_Agents_MovingAway_RO_ComponentLookup;

			// Token: 0x040093EB RID: 37867
			[ReadOnly]
			public ComponentLookup<TouristHousehold> __Game_Citizens_TouristHousehold_RO_ComponentLookup;

			// Token: 0x040093EC RID: 37868
			[ReadOnly]
			public ComponentLookup<HomelessHousehold> __Game_Citizens_HomelessHousehold_RO_ComponentLookup;

			// Token: 0x040093ED RID: 37869
			[ReadOnly]
			public ComponentLookup<Game.Objects.OutsideConnection> __Game_Objects_OutsideConnection_RO_ComponentLookup;

			// Token: 0x040093EE RID: 37870
			[ReadOnly]
			public ComponentLookup<InDanger> __Game_Events_InDanger_RO_ComponentLookup;

			// Token: 0x040093EF RID: 37871
			[ReadOnly]
			public BufferLookup<CoordinatedMeetingAttendee> __Game_Citizens_CoordinatedMeetingAttendee_RO_BufferLookup;

			// Token: 0x040093F0 RID: 37872
			public ComponentLookup<CoordinatedMeeting> __Game_Citizens_CoordinatedMeeting_RW_ComponentLookup;

			// Token: 0x040093F1 RID: 37873
			[ReadOnly]
			public BufferLookup<HaveCoordinatedMeetingData> __Game_Prefabs_HaveCoordinatedMeetingData_RO_BufferLookup;

			// Token: 0x040093F2 RID: 37874
			[ReadOnly]
			public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

			// Token: 0x040093F3 RID: 37875
			[ReadOnly]
			public BufferLookup<Game.Buildings.Student> __Game_Buildings_Student_RO_BufferLookup;

			// Token: 0x040093F4 RID: 37876
			[ReadOnly]
			public ComponentLookup<Population> __Game_City_Population_RO_ComponentLookup;

			// Token: 0x040093F5 RID: 37877
			[ReadOnly]
			public ComponentLookup<OutsideConnectionData> __Game_Prefabs_OutsideConnectionData_RO_ComponentLookup;

			// Token: 0x040093F6 RID: 37878
			[ReadOnly]
			public BufferLookup<OwnedVehicle> __Game_Vehicles_OwnedVehicle_RO_BufferLookup;

			// Token: 0x040093F7 RID: 37879
			[ReadOnly]
			public ComponentLookup<CommuterHousehold> __Game_Citizens_CommuterHousehold_RO_ComponentLookup;

			// Token: 0x040093F8 RID: 37880
			public ComponentLookup<CarKeeper> __Game_Citizens_CarKeeper_RW_ComponentLookup;

			// Token: 0x040093F9 RID: 37881
			[ReadOnly]
			public ComponentLookup<HouseholdMember> __Game_Citizens_HouseholdMember_RO_ComponentLookup;

			// Token: 0x040093FA RID: 37882
			[ReadOnly]
			public BufferLookup<DistrictModifier> __Game_Areas_DistrictModifier_RO_BufferLookup;

			// Token: 0x040093FB RID: 37883
			[ReadOnly]
			public ComponentLookup<CurrentDistrict> __Game_Areas_CurrentDistrict_RO_ComponentLookup;

			// Token: 0x040093FC RID: 37884
			[ReadOnly]
			public ComponentLookup<Citizen> __Game_Citizens_Citizen_RO_ComponentLookup;

			// Token: 0x040093FD RID: 37885
			[ReadOnly]
			public ComponentLookup<BicycleOwner> __Game_Citizens_BicycleOwner_RO_ComponentLookup;

			// Token: 0x040093FE RID: 37886
			[ReadOnly]
			public ComponentLookup<CurrentBuilding> __Game_Citizens_CurrentBuilding_RO_ComponentLookup;

			// Token: 0x040093FF RID: 37887
			[ReadOnly]
			public ComponentLookup<SpawnableBuildingData> __Game_Prefabs_SpawnableBuildingData_RO_ComponentLookup;

			// Token: 0x04009400 RID: 37888
			[ReadOnly]
			public ComponentLookup<MailAccumulationData> __Game_Prefabs_MailAccumulationData_RO_ComponentLookup;

			// Token: 0x04009401 RID: 37889
			[ReadOnly]
			public ComponentLookup<ServiceObjectData> __Game_Prefabs_ServiceObjectData_RO_ComponentLookup;

			// Token: 0x04009402 RID: 37890
			public ComponentLookup<MailSender> __Game_Citizens_MailSender_RW_ComponentLookup;

			// Token: 0x04009403 RID: 37891
			public ComponentLookup<MailProducer> __Game_Buildings_MailProducer_RW_ComponentLookup;

			// Token: 0x04009404 RID: 37892
			public ComponentLookup<CitizenPresence> __Game_Buildings_CitizenPresence_RW_ComponentLookup;
		}
	}
}
