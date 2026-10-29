using BitulaMod.Components;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using static Unity.Burst.Intrinsics.X86.Avx;

namespace BitulaMod.SmallCity {
    public struct SCCommon {
        private SmallCityJobsPhase m_Phase;
        private Unity.Mathematics.Random m_Random;
        private ComponentLookup<CmpSmallCityJobs> m_SmallCitySearch;
        private ComponentLookup<Citizen> m_Citizens;
        private ComponentLookup<HouseholdMember> m_HouseholdMembers;
        private ComponentLookup<Followed> m_Followed;
        private ComponentLookup<Building> m_Buildings;
        private ComponentLookup<Game.Objects.OutsideConnection> m_OutsideConnections;
        private ComponentLookup<Worker> m_Workers;
        private ComponentLookup<Game.Citizens.Student> m_Students;
        private EconomyParameterData m_EconomyParameters;
        private float m_NormalizedTime;
        private uint m_SimulationFrame;
        private BufferLookup<Game.Economy.Resources> m_Resources;
        private EntityCommandBuffer.ParallelWriter m_CommandBuffer;
        private NativeQueue<CustomEvent>.ParallelWriter m_CustomEventQueue;
        private FixedString512Bytes m_Parameters;
        private FixedList512Bytes<CustomEventType> m_WatchedEvents;
        private EventHint m_WatchHint;
        private EventHint m_Hint;
        public const uint kFramesPerDay = 262144u;
        private int m_CommandBufferIndex;
        private bool m_HasCommandBufferIndex;


        public EntityCommandBuffer.ParallelWriter CommandBuffer => m_CommandBuffer;
        public static SCCommon Create(ref SystemState state, EntityCommandBuffer.ParallelWriter? commandBuffer = null) {

            var eventSender = state.World.GetOrCreateSystemManaged<LifePathEventSenderSystem>();

            var smallCitySeed = (uint)System.Environment.TickCount;
            if (smallCitySeed == 0)
                smallCitySeed = 1;

            return new SCCommon {
                m_CommandBuffer = commandBuffer.GetValueOrDefault(),
                m_CustomEventQueue = eventSender.GetQueueWriter(),
                m_Phase = SmallCityJobsPhase.None,
                m_Random = new Unity.Mathematics.Random(smallCitySeed),
                m_Citizens = state.GetComponentLookup<Citizen>(true),
                m_Followed = state.GetComponentLookup<Followed>(true),
                m_SmallCitySearch = state.GetComponentLookup<CmpSmallCityJobs>(true),
                m_HouseholdMembers = state.GetComponentLookup<HouseholdMember>(true),
                m_Resources = state.GetBufferLookup<Game.Economy.Resources>(true),
                m_Buildings = state.GetComponentLookup<Building>(true),
                m_OutsideConnections = state.GetComponentLookup<Game.Objects.OutsideConnection>(true),
                m_SimulationFrame = state.World.GetOrCreateSystemManaged<SimulationSystem>().frameIndex,
                m_Workers = state.GetComponentLookup<Worker>(true),
                m_Students = state.GetComponentLookup<Game.Citizens.Student>(true), m_EconomyParameters = state.GetEntityQuery(
                    ComponentType.ReadOnly<EconomyParameterData>()).GetSingleton<EconomyParameterData>(),
                m_NormalizedTime = state.World.GetOrCreateSystemManaged<TimeSystem>().normalizedTime,
            };
        }

        public void Update(ref SystemState state) {
            m_Citizens.Update(ref state);
            m_Followed.Update(ref state);
            m_SmallCitySearch.Update(ref state);
            m_HouseholdMembers.Update(ref state);
            m_Resources.Update(ref state);
            m_Buildings.Update(ref state);
            m_OutsideConnections.Update(ref state);
            m_Workers.Update(ref state);
            m_Students.Update(ref state);

            m_SimulationFrame =
                state.World.GetOrCreateSystemManaged<SimulationSystem>().frameIndex;

            m_NormalizedTime =
                state.World.GetOrCreateSystemManaged<TimeSystem>().normalizedTime;

            m_EconomyParameters = state.GetEntityQuery(
                ComponentType.ReadOnly<EconomyParameterData>()
            ).GetSingleton<EconomyParameterData>();
        }

        public bool IsSmallCityBehaviour(Entity citizen) {
            return citizen != Entity.Null &&
                   m_SmallCitySearch.HasComponent(citizen) &&
                   m_SmallCitySearch[citizen].m_UseSmallCityBehaviour;
        }

        public bool HasSmallCityBehaviour(Entity citizen) {
            return citizen != Entity.Null && m_SmallCitySearch.HasComponent(citizen);
        }

        public EconomyParameterData GetEconomyParameterData() {
            return m_EconomyParameters;
        }

        public bool CitizenExists(Entity citizenEntity) {
            return citizenEntity != Entity.Null &&
                   m_Citizens.HasComponent(citizenEntity);
        }

        public void SetCommandBuffer(EntityCommandBuffer.ParallelWriter buffer, int index) {
            m_CommandBuffer = buffer;
            m_CommandBufferIndex = index;
            m_HasCommandBufferIndex = true;
        }
        public int GetCommandBufferIndex(Entity citizen) {
            return m_HasCommandBufferIndex ? m_CommandBufferIndex : citizen.Index;
        }

        public void SetCmp<T>(Entity citizen, T cmp) where T : unmanaged, IComponentData {
            m_CommandBuffer.SetComponent(GetCommandBufferIndex(citizen), citizen, cmp);
        }

        public void AddCmp<T>(Entity citizen, T cmp) where T : unmanaged, IComponentData {
            m_CommandBuffer.AddComponent(GetCommandBufferIndex(citizen), citizen, cmp);
        }

        public void AddOrSetCmp<T>(Entity citizen, T cmp) where T : unmanaged, IComponentData {
            m_CommandBuffer.AddComponent(GetCommandBufferIndex(citizen), citizen, cmp);
        }

        public uint GetSimulationFrame() {
            return m_SimulationFrame;
        }

        public uint GetLastPossibleFrame(Entity citizen) {
            bool isWorker = m_Workers.TryGetComponent(citizen, out Worker worker);
            bool isStudent = m_Students.TryGetComponent(citizen, out Game.Citizens.Student student);
            Citizen citizenData = GetCitizenData(citizen);
            float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(
                citizen, citizenData, ref m_EconomyParameters,
                isWorker, worker, isStudent, student);

            float timeLeft = GetTimeLeftUntilInterval(sleepTime);

            if (isWorker) {
                float2 timeToWork = WorkerSystem.GetTimeToWork(
                    citizenData, worker, ref m_EconomyParameters, true);

                timeLeft = math.min(timeLeft, GetTimeLeftUntilInterval(timeToWork));
            } else if (isStudent) {
                float2 timeToStudy = StudentSystem.GetTimeToStudy(
                    citizenData, student, ref m_EconomyParameters);

                timeLeft = math.min(timeLeft, GetTimeLeftUntilInterval(timeToStudy));
            }

            return m_SimulationFrame + (uint)(timeLeft * kFramesPerDay);
        }

        private float GetTimeLeftUntilInterval(float2 interval) {
            if (m_NormalizedTime >= interval.x)
                return 1f - m_NormalizedTime + interval.x;

            return interval.x - m_NormalizedTime;
        }

        public static ref Unity.Mathematics.Random GetRandom(ref SCCommon common) {
            return ref common.m_Random;
        }

        public Citizen GetCitizenData(Entity citizen) {
            return m_Citizens[citizen];
        }

        public CmpSmallCityJobs GetSmallCitiyJobs(Entity citizen) {
            return m_SmallCitySearch[citizen];
        }

        public bool HasSmallCitiyJobs(Entity citizen) {
            return m_SmallCitySearch.HasComponent(citizen);
         }

        public SmallCityJobsPhase GetPhase() { 
            return m_Phase; 
        }

        public bool IsFollowed(Entity citizen) {
            return m_Followed.HasComponent(citizen);
        }
        public bool HasBuilding(Entity workplace) {
            return m_Buildings.HasComponent(workplace);
        }

        public bool HasOutsideConnection(Entity workplace) {
            return m_OutsideConnections.HasComponent(workplace);
        }


        public int EducationLevel(Entity citizen) {
            if (!m_Citizens.HasComponent(citizen))
                return -1;

            return m_Citizens[citizen].GetEducationLevel();
        }

        public void Send(Entity citizen, CustomEventType eventType, Entity? target = null) {
            if (IsFollowed(citizen)) {
                m_CustomEventQueue.Enqueue(new CustomEvent {
                    m_Citizen = citizen,
                    m_EventType = eventType,
                    m_Param = m_Parameters,
                    m_Hint = this.m_Hint,
                    m_WatchHint = this.m_WatchHint,
                    m_WatchedEventTypes = m_WatchedEvents,
                    m_Target = target ?? Entity.Null,
                });
            }

            m_Parameters = default;
            m_Hint = default;
            m_WatchedEvents = default;
            m_WatchHint = default;
        }

        public void AddParameter(int parameter) {
            if (!m_Parameters.IsEmpty) {
                m_Parameters.Append(',');
            }

            m_Parameters.Append(parameter);
        }

        public void AddParameter(FixedString512Bytes parameter) {
            if (!m_Parameters.IsEmpty) {
                m_Parameters.Append(',');
            }

            m_Parameters.Append(parameter);
        }

        public void SetWatchHint(EventHint hint) {
            m_WatchHint |= hint;
        }

        public void SetHint(EventHint hint) {
            m_Hint |= hint;
        }

        public void SetEventToWatch(CustomEventType eventType) {
            for (int i = 0; i < m_WatchedEvents.Length; i++) {
                if (m_WatchedEvents[i] == eventType)
                    return;
            }

            m_WatchedEvents.Add(eventType);
        }

        public ComponentLookup<HouseholdMember> GetHouseholdMembers() { 
            return m_HouseholdMembers; 
        }

        public BufferLookup<Game.Economy.Resources> GetResources() {
            return m_Resources;
        }




    }
}
