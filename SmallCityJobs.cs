using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Game.Common;
using UnityEngine;
using Game.Tools;
using System;
using System.Globalization;

namespace BitulaMod
{
    public struct CloserJobSearch : IComponentData {
    }
    public struct SmallCityJobs
    {
        private const int VanillaWorkspaceThreshold = 100;
        private int m_JobSeekerMilestone;
        private int m_JobSeekerFailureIncrement;
        private bool m_AcceptLowerJobs;
        private bool m_AcceptSwitchJobs;
        private bool m_ReducedDaysOff;
        private Entity m_City;
        private FixedString64Bytes m_Parameters;
        private byte m_Hint;
        private CustomEventType m_WatchedEvent;
        private ComponentLookup<Population> m_Population;
        private ComponentLookup<Followed> m_Followed;
        private ComponentLookup<Building> m_Buildings;
        private ComponentLookup<CompanyData> m_CompanyDatas;
        private ComponentLookup<Worker> m_Workers;
        private ComponentLookup<HouseholdMember> m_HouseholdMembers;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        private ComponentLookup<Game.Objects.Transform> m_Transforms;
        private ComponentLookup<FreeWorkplaces> m_FreeWorkplaces;
        private ComponentLookup<CloserJobSearch> m_CloserJobSearch;
        private EntityCommandBuffer.ParallelWriter m_CommandBuffer;

        [ReadOnly]
        public NativeArray<Entity> m_WorkplaceEntities;

        private NativeQueue<CustomEvent>.ParallelWriter m_CustomEventQueue;

        public static SmallCityJobs Create(ref SystemState state, EntityCommandBuffer.ParallelWriter? commandBuffer = null) {
            var cityQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Population>()
            );

            var eventSender =
                state.World.GetOrCreateSystemManaged<LifePathEventSenderSystem>();

            var workplaceQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<FreeWorkplaces>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Destroyed>()
            );

            return new SmallCityJobs {
                m_Population = state.GetComponentLookup<Population>(true),
                m_Followed = state.GetComponentLookup<Followed>(true),
                m_City = cityQuery.GetSingletonEntity(),
                m_Buildings = state.GetComponentLookup<Building>(true),
                m_CompanyDatas = state.GetComponentLookup<CompanyData>(true),

                m_Workers = state.GetComponentLookup<Worker>(true),
                m_HouseholdMembers = state.GetComponentLookup<HouseholdMember>(true),
                m_PropertyRenters = state.GetComponentLookup<PropertyRenter>(true),
                m_Transforms = state.GetComponentLookup<Game.Objects.Transform>(true),
                m_FreeWorkplaces = state.GetComponentLookup<FreeWorkplaces>(true),
                m_CloserJobSearch = state.GetComponentLookup<CloserJobSearch>(true),

                m_WorkplaceEntities = workplaceQuery.ToEntityArray(state.WorldUpdateAllocator),

                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_AcceptLowerJobs = Mod.Settings.AcceptLowerJobs,
                m_AcceptSwitchJobs = Mod.Settings.AcceptJobSwitch,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff,
                m_CustomEventQueue = eventSender.GetQueueWriter(),

                m_CommandBuffer = commandBuffer.GetValueOrDefault()
            };
        }

        public static SmallCityJobs Create() {
            return new SmallCityJobs {
                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff
            };
        }

        public void AddParameter(int parameter) {
            if (!m_Parameters.IsEmpty) {
                m_Parameters.Append(',');
            }

            m_Parameters.Append(parameter);
        }

        public void AddParameter(FixedString64Bytes parameter) {
            m_Parameters.Append(parameter);
        }

        public void Send(Entity citizen, CustomEventType eventType) {
            if (IsFollowed(citizen)) {
                m_CustomEventQueue.Enqueue(new CustomEvent {
                    m_Citizen = citizen,
                    m_EventType = eventType,
                    m_Param = m_Parameters,
                    m_Hint = this.m_Hint,
                    m_WatchedEventType = m_WatchedEvent
                });
            }

            m_Parameters = default;
            m_Hint = default;
            m_WatchedEvent = default;
        }

        public static void AddProducer(ref SystemState state, JobHandle dependency)
        {
            var eventSender =
                state.World.GetOrCreateSystemManaged<LifePathEventSenderSystem>();

            eventSender.AddProducer(dependency);
        }

        public bool IsFollowed(Entity citizen)
        {
            return m_Followed.HasComponent(citizen);
        }

        public bool IsCompany(Entity workplace) {
            return m_CompanyDatas.HasComponent(workplace);
        }

        public bool HasBuilding(Entity workplace) {
            return m_Buildings.HasComponent(workplace);
        }

        public bool RemoveOvereducationPenalty(ref Unity.Mathematics.Random random) {
            if (!m_AcceptLowerJobs)
                return false;

            int population = m_Population[m_City].m_Population;
            int passedMilestones =
                math.max(0, population - 1) / m_JobSeekerMilestone;

            int protectionPercentage = math.max(
                0,
                100 - passedMilestones * m_JobSeekerFailureIncrement);

            return random.NextInt(100) < protectionPercentage;
        }

        public bool FailedJobApplication(int numJobs, ref Unity.Mathematics.Random random) {
            if (numJobs <= 0)
                return true;

            int population = m_Population[m_City].m_Population;

            int passedMilestones = math.max(0, population - 1)
                / m_JobSeekerMilestone;

            int appliedFailurePercentage = math.min(
                100,
                passedMilestones * m_JobSeekerFailureIncrement);

            bool applicationFailed =
                numJobs < random.NextInt(100);

            return applicationFailed
                && random.NextInt(100) < appliedFailurePercentage;
        }

        private int GetProgression() {
            int population = m_Population[m_City].m_Population;
            int passedMilestones = math.max(0, population - 1)
                / m_JobSeekerMilestone;

            return math.min(100, passedMilestones * m_JobSeekerFailureIncrement);
        }

        private bool UseSmallCityBehavior(ref Unity.Mathematics.Random random) {
            return random.NextInt(100) >= GetProgression();
        }

        public bool FoundCloserJob(Entity citizen) {
            return m_CloserJobSearch.HasComponent(citizen);
        }

        public void RemoveFoundCloserJob(Entity citizen) {
            m_CommandBuffer.RemoveComponent<CloserJobSearch>(citizen.Index, citizen);
        }

        public bool SkippedJobApplication(int numJobs, int currentJobLevel,
            int highestAvailableJobLevel, ref Unity.Mathematics.Random random, Entity citizen) {

            if (numJobs <= 0)
                return true;

            bool vanillaSkipped =
                numJobs <= VanillaWorkspaceThreshold ||
                numJobs < random.NextInt(500);

            if (!m_AcceptSwitchJobs)
                return vanillaSkipped;

            bool useSmallCityBehavior = UseSmallCityBehavior(ref random);
            bool hasBetterJob = highestAvailableJobLevel > currentJobLevel;            


            if (!hasBetterJob && useSmallCityBehavior) {
                hasBetterJob = HasClosestSameLevelJob(citizen, currentJobLevel);

                if (hasBetterJob) {
                    m_CommandBuffer.AddComponent<CloserJobSearch>(citizen.Index, citizen);

                    if (IsFollowed(citizen))
                        Send(citizen, CustomEventType.FoundCloserJob);
                }
            }

            if (!hasBetterJob)
                return vanillaSkipped || useSmallCityBehavior;

            return vanillaSkipped && !useSmallCityBehavior;
        }

        public void SetHint(byte hint) {
            m_Hint |= hint;
        }


        public void SendOnlyIfWatchedEvent(CustomEventType eventType) {
            m_WatchedEvent = eventType;
        }

        public bool IsTodayOffDay(Citizen citizen, ref EconomyParameterData economyParameters, uint frame, TimeData timeData, int population) {
            int vanillaThreshold = math.min(40, Mathf.RoundToInt(100f / math.max(1f, math.sqrt(economyParameters.m_TrafficReduction * (float)population))));
            int threshold = vanillaThreshold;

            if (m_ReducedDaysOff) {
                int passedMilestones = math.max(0, population - 1) / m_JobSeekerMilestone;
                int appliedPercentage = math.min(100, passedMilestones * m_JobSeekerFailureIncrement);

                const int smallCityThreshold = 79; // 20% off days

                threshold = Mathf.RoundToInt(math.lerp(
                    smallCityThreshold,
                    vanillaThreshold,
                    appliedPercentage / 100f));
            }

            int day = TimeSystem.GetDay(frame, timeData);

            return Unity.Mathematics.Random
                .CreateFromIndex((uint)((int)citizen.m_PseudoRandom + day))
                .NextInt(100) > threshold;
        }

        public bool HasClosestSameLevelJob(Entity citizen, int currentJobLevel) {
            return GetClosestSameLevelJob(citizen, currentJobLevel) != Entity.Null;
        }

        

        public Entity GetClosestSameLevelJob(Entity citizen, int currentJobLevel) {
            if (currentJobLevel < 0 || !m_Workers.HasComponent(citizen) || !m_HouseholdMembers.HasComponent(citizen)) {
                return Entity.Null;
            }

            Worker worker = m_Workers[citizen];
            Entity household = m_HouseholdMembers[citizen].m_Household;

            if (!m_PropertyRenters.HasComponent(household)) {
                return Entity.Null;
            }

            Entity home = m_PropertyRenters[household].m_Property;

            Entity currentWorkplace = worker.m_Workplace;
            Entity currentWorkplaceBuilding = m_PropertyRenters.HasComponent(currentWorkplace)
                ? m_PropertyRenters[currentWorkplace].m_Property
                : currentWorkplace;

            if (!m_Transforms.HasComponent(home) ||
                !m_Transforms.HasComponent(currentWorkplaceBuilding)) {
                return Entity.Null;
            }

            var homePosition = m_Transforms[home].m_Position;
            var currentPosition = m_Transforms[currentWorkplaceBuilding].m_Position;

            float closestDistance = Unity.Mathematics.math.distancesq(
                homePosition.xz,
                currentPosition.xz);

            Entity closestWorkplace = Entity.Null;

            for (int i = 0; i < m_WorkplaceEntities.Length; i++) {
                Entity workplace = m_WorkplaceEntities[i];

                if (workplace == currentWorkplace ||
                    !m_FreeWorkplaces.HasComponent(workplace)) {
                    continue;
                }

                FreeWorkplaces freeWorkplaces = m_FreeWorkplaces[workplace];

                if (GetFreeWorkplacesAtLevel(freeWorkplaces, currentJobLevel) <= 0) {
                    continue;
                }

                Entity workplaceBuilding = m_PropertyRenters.HasComponent(workplace)
                    ? m_PropertyRenters[workplace].m_Property
                    : workplace;

                if (!m_Transforms.HasComponent(workplaceBuilding)) {
                    continue;
                }

                var workplacePosition = m_Transforms[workplaceBuilding].m_Position;

                float distance = Unity.Mathematics.math.distancesq(
                    homePosition.xz,
                    workplacePosition.xz);

                if (distance < closestDistance) {
                    closestDistance = distance;
                    closestWorkplace = workplace;
                }
            }

            return closestWorkplace;
        }

        private static int GetFreeWorkplacesAtLevel(FreeWorkplaces workplaces, int level) {
            switch (level) {
                case 0:
                    return workplaces.m_Uneducated;
                case 1:
                    return workplaces.m_PoorlyEducated;
                case 2:
                    return workplaces.m_Educated;
                case 3:
                    return workplaces.m_WellEducated;
                case 4:
                    return workplaces.m_HighlyEducated;
                default:
                    return 0;
            }
        }


    }
}
