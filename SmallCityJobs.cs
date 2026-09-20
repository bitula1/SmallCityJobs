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
using Game.Agents;

namespace BitulaMod
{
    
    public struct SmallCityJobs
    {
        private const int VanillaWorkspaceThreshold = 100;
        private const int VanillaSwitchDesireThreshold = 500;
        private Entity m_Citizen;
        private bool m_UseSmallCityBehaviour;
        private SmallCityJobsPhase m_Phase;
        private int m_JobSeekerMilestone;
        private int m_JobSeekerFailureIncrement;
        private bool m_AcceptLowerJobs;
        private bool m_AcceptSwitchJobs;
        private bool m_ReducedDaysOff;
        private bool m_PromoteJob;
        private bool m_AcceptCloserJobsEmployed;
        private bool m_AcceptCloserJobsUnemployed;
        private Unity.Mathematics.Random m_Random;
        private Entity m_City;
        private FixedString64Bytes m_Parameters;
        private byte m_Hint;
        private Workplaces m_Workplaces;
        private CustomEventType m_WatchedEvent;
        private ComponentLookup<Population> m_Population;
        private ComponentLookup<Followed> m_Followed;
        private ComponentLookup<Building> m_Buildings;
        private ComponentLookup<CompanyData> m_CompanyDatas;
        private ComponentLookup<Worker> m_Workers;
        private ComponentLookup<Citizen> m_Citizens;
        private ComponentLookup<HouseholdMember> m_HouseholdMembers;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        private ComponentLookup<Game.Objects.Transform> m_Transforms;
        private ComponentLookup<FreeWorkplaces> m_FreeWorkplaces;
        private ComponentLookup<SmallCityJobsComponent> m_SmallCitySearch;
        private ComponentLookup<TravelPurpose> m_TravelPurposes;


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

            var countWorkplacesSystem = state.World.GetOrCreateSystemManaged<CountWorkplacesSystem>();
            Workplaces localFreeWorkplaces = countWorkplacesSystem.GetFreeWorkplaces();

            var workplaceQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<FreeWorkplaces>(),
                ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(),
                ComponentType.Exclude<Destroyed>()
            );

            var smallCityJobsSeed = (uint)System.Environment.TickCount;
            if (smallCityJobsSeed == 0)
                smallCityJobsSeed = 1;

            return new SmallCityJobs {
                m_Citizen = default,
                m_Phase = SmallCityJobsPhase.None,
                m_UseSmallCityBehaviour = false,
                m_Population = state.GetComponentLookup<Population>(true),
                m_Followed = state.GetComponentLookup<Followed>(true),
                m_City = cityQuery.GetSingletonEntity(),
                m_Buildings = state.GetComponentLookup<Building>(true),
                m_CompanyDatas = state.GetComponentLookup<CompanyData>(true),
                m_Workers = state.GetComponentLookup<Worker>(true),
                m_Citizens = state.GetComponentLookup<Citizen>(true),
                m_HouseholdMembers = state.GetComponentLookup<HouseholdMember>(true),
                m_PropertyRenters = state.GetComponentLookup<PropertyRenter>(true),
                m_Transforms = state.GetComponentLookup<Game.Objects.Transform>(true),
                m_FreeWorkplaces = state.GetComponentLookup<FreeWorkplaces>(true),
                m_SmallCitySearch = state.GetComponentLookup<SmallCityJobsComponent>(true),
                m_TravelPurposes = state.GetComponentLookup<TravelPurpose>(true),

                m_WorkplaceEntities = workplaceQuery.ToEntityArray(state.WorldUpdateAllocator),
                m_Random = new Unity.Mathematics.Random(smallCityJobsSeed),

                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_AcceptLowerJobs = Mod.Settings.AcceptLowerJobs,
                m_AcceptSwitchJobs = Mod.Settings.AcceptJobSwitch,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff,
                m_PromoteJob = Mod.Settings.WorkplacePromotion,
                m_AcceptCloserJobsEmployed = Mod.Settings.CloserJobEmployed,
                m_AcceptCloserJobsUnemployed = Mod.Settings.CloserJobUnemployed,
                m_CustomEventQueue = eventSender.GetQueueWriter(),
                m_Workplaces = localFreeWorkplaces,
                m_CommandBuffer = commandBuffer.GetValueOrDefault()
            };
        }

        public int GetFreeWorkplaces(int level) {
            return m_Workplaces[level];
        }

        public static SmallCityJobs Create() {
            return new SmallCityJobs {
                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff,
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

        public bool isEmployed(Entity citizen) {
            return m_Workers.HasComponent(citizen);
        }

        public bool IsIdle(Entity citizen) {
            return !m_TravelPurposes.HasComponent(citizen);
        }

        public bool UnemployedSkippedApplication(int numJobs, Entity citizen) {
            if (numJobs <= 0) {
                Send(citizen, CustomEventType.NoJobsAvailable);
                return true;
            }

            bool useSmallCityBehavior = UseSmallCityBehavior(citizen);

            bool applicationFailed =
                numJobs < m_Random.NextInt(VanillaWorkspaceThreshold);
            bool failed = applicationFailed && !useSmallCityBehavior;

            if (!failed && useSmallCityBehavior && m_AcceptCloserJobsUnemployed) {
                SmallCityJobsComponent cmp = m_SmallCitySearch[citizen];
                cmp.m_FoundCloserJob = true;
                cmp.m_FoundHigherJob = false;
                cmp.m_UseSmallCityBehaviour = m_UseSmallCityBehaviour;
                cmp.m_phase = m_Phase;
                m_CommandBuffer.SetComponent(citizen.Index, citizen, cmp);
            } else if (failed) {
                Send(citizen, CustomEventType.DoesntLikeAnyJobs);
            }

            return failed;
        }

        private int GetProgression() {
            int population = m_Population[m_City].m_Population;
            int passedMilestones = math.max(0, population - 1)
                / m_JobSeekerMilestone;

            return math.min(100, passedMilestones * m_JobSeekerFailureIncrement);
        }

        public bool init(Entity citizen, SmallCityJobsPhase phase) {
            
            if (!m_SmallCitySearch.HasComponent(citizen)) {
                return false;
            } else if (phase == SmallCityJobsPhase.LookingForJob && (m_Citizen == Entity.Null || m_Citizen != citizen)) {
                bool rnd = m_Random.NextInt(100) >= GetProgression();
                SmallCityJobsComponent cmp = m_SmallCitySearch[citizen];
                cmp.m_phase = phase;
                cmp.m_UseSmallCityBehaviour = rnd;
                m_UseSmallCityBehaviour = rnd;
                cmp.m_FoundCloserJob = false;
                cmp.m_FoundHigherJob = false;
                m_CommandBuffer.SetComponent(citizen.Index, citizen, cmp);
            } else if (phase != SmallCityJobsPhase.LookingForJob) {
                m_UseSmallCityBehaviour = m_SmallCitySearch[citizen].m_UseSmallCityBehaviour;
            }
            m_Phase = phase;            
            m_Citizen = citizen;
            return true;

        }

        public bool UseSmallCityBehavior(Entity citizen) {                        
            return m_UseSmallCityBehaviour;
        }

        public bool FoundCloserJob(Entity citizen) {
            return m_SmallCitySearch.HasComponent(citizen) && m_SmallCitySearch[citizen].m_FoundCloserJob;
        }

        public bool FoundHigherJob(Entity citizen) {
            return m_SmallCitySearch.HasComponent(citizen) && m_SmallCitySearch[citizen].m_FoundHigherJob;
        }


        public void RemoveSmallCityJobs(Entity citizen) {
            m_CommandBuffer.RemoveComponent<SmallCityJobsComponent>(citizen.Index, citizen);
        }

        public bool EmployedSkippedApplication(int numJobs, int currentJobLevel, int highestAvailableJobLevel, Entity citizen) {

            if (!IsIdle(citizen)) return true;

            if (numJobs <= 0) {
                Send(citizen, CustomEventType.CantSwitchJob);
                return true;
            }

            bool useSmallCityBehavior = UseSmallCityBehavior(citizen);
            bool vanillaSkipped = numJobs <= VanillaWorkspaceThreshold || numJobs < m_Random.NextInt(VanillaSwitchDesireThreshold);
            
            if (!m_AcceptSwitchJobs)
                return vanillaSkipped;
            
            bool hasBetterJob = highestAvailableJobLevel > currentJobLevel;
            bool hasCloserJob = useSmallCityBehavior && m_AcceptCloserJobsEmployed && HasClosestJob(citizen, currentJobLevel);

            if (useSmallCityBehavior) {
                SmallCityJobsComponent cmp = m_SmallCitySearch[citizen];
                cmp.m_FoundHigherJob = hasBetterJob;
                cmp.m_FoundCloserJob = hasCloserJob;
                cmp.m_UseSmallCityBehaviour = m_UseSmallCityBehaviour;
                cmp.m_phase = m_Phase;
                m_CommandBuffer.SetComponent(citizen.Index, citizen, cmp);
                if (hasCloserJob) Send(citizen, CustomEventType.FoundCloserJob);
            }

            if (hasBetterJob && vanillaSkipped && !useSmallCityBehavior) {
                if (numJobs <= VanillaWorkspaceThreshold) {
                    AddParameter(numJobs);
                    SetHint(CustomEvent.IgnoreParameterInFilter);
                    Send(citizen, CustomEventType.TooFewBetterJobs);
                } else {
                    AddParameter(numJobs);
                    Send(citizen, CustomEventType.StartedLookingForAnotherJob);
                    Send(citizen, CustomEventType.DoesntWantBetterJob);
                }
            }
            bool noSuitableJobs = !hasBetterJob && !hasCloserJob;
            
            if (noSuitableJobs)
                Send(citizen, CustomEventType.NoSuitableSwitch);

            if (!useSmallCityBehavior)
                return vanillaSkipped;

            return noSuitableJobs;
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

        public int EducationLevel(Entity citizen) {
            if (!m_Citizens.HasComponent(citizen))
                return -1;

            return m_Citizens[citizen].GetEducationLevel();
        }

        public bool HasClosestJob(Entity citizen, int currentJobLevel) {
            return GetClosestJob(citizen, currentJobLevel ) != Entity.Null;
        }

        public Entity GetClosestJob(Entity citizen, int currentJobLevel) {
            if (isEmployed(citizen)) return GetClosestSameLevelJobEmployed(citizen, currentJobLevel);
            return GetClosestJobUnemployed(citizen, EducationLevel(citizen) );
        }

        public Entity GetClosestJobUnemployed(Entity citizen, int educationLevel) {
            if (!m_HouseholdMembers.HasComponent(citizen))
                return Entity.Null;

            Entity household = m_HouseholdMembers[citizen].m_Household;

            if (!m_PropertyRenters.HasComponent(household))
                return Entity.Null;

            Entity home = m_PropertyRenters[household].m_Property;

            if (!m_Transforms.HasComponent(home))
                return Entity.Null;

            var homePosition = m_Transforms[home].m_Position;

            float closestDistance = float.MaxValue;
            Entity closestWorkplace = Entity.Null;

            for (int i = 0; i < m_WorkplaceEntities.Length; i++) {
                Entity workplace = m_WorkplaceEntities[i];

                if (!m_FreeWorkplaces.HasComponent(workplace))
                    continue;

                FreeWorkplaces freeWorkplaces = m_FreeWorkplaces[workplace];

                if (freeWorkplaces.Count <= 0)
                    continue;

                if (educationLevel < (int)freeWorkplaces.GetLowestFree())
                    continue;

                Entity workplaceBuilding = m_PropertyRenters.HasComponent(workplace)
                    ? m_PropertyRenters[workplace].m_Property
                    : workplace;

                if (!m_Transforms.HasComponent(workplaceBuilding))
                    continue;

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

        public Entity GetClosestSameLevelJobEmployed(Entity citizen, int currentJobLevel) {
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

        public bool AcceptLowerJobs() {
            return m_AcceptLowerJobs;
        }

        public bool IsWorkplaceAllowed(Entity owner, Entity destination, ComponentLookup<PrefabRef> prefabs) {
            if (!prefabs.HasComponent(destination))
                return false;
            bool sameWorkplace = m_Workers.HasComponent(owner) &&  destination == m_Workers[owner].m_Workplace;
            if (sameWorkplace && m_PromoteJob)  return true;

            return !sameWorkplace;
        }

        public bool IsPromotionAllowed(Entity owner, Entity destination, ComponentLookup<PrefabRef> prefabs, int bestFor) {
            bool sameWorkplace =  m_Workers.HasComponent(owner) &&  destination == m_Workers[owner].m_Workplace;

            if (!prefabs.HasComponent(destination))
                return false;

            return sameWorkplace && m_PromoteJob && bestFor > this.m_Workers[owner].m_Level;
        }

        public void PromoteWorker(Entity owner, DynamicBuffer<Employee> dynamicBuffer, int bestFor, EntityCommandBuffer commandBuffer) {

            for (int k = 0; k < dynamicBuffer.Length; k++) {
                Employee employee = dynamicBuffer[k];

                if (employee.m_Worker == owner) {
                    employee.m_Level = (byte)bestFor;
                    dynamicBuffer[k] = employee;

                    Worker worker = this.m_Workers[owner];
                    worker.m_Level = (byte)bestFor;
                    commandBuffer.SetComponent(owner, worker);

                    Send(owner, CustomEventType.PromotedJob);
                    return;
                }
            }
        }


    }
}
