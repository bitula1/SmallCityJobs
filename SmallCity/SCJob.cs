using BitulaMod.Components;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BitulaMod.SmallCity {
    public struct SCJob : ISCFeature<SCJob> {
        private const int VanillaWorkspaceThreshold = 100;
        private const int VanillaSwitchDesireThreshold = 500;
        private Workplaces m_Workplaces;
        private SCCommon m_Common;
        private NativeArray<Entity> m_WorkplaceEntities;
        private ComponentLookup<Worker> m_Workers;
        private ComponentLookup<CompanyData> m_CompanyDatas;
        private ComponentLookup<FreeWorkplaces> m_FreeWorkplaces;
        private ComponentLookup<TravelPurpose> m_TravelPurposes;
        private ComponentLookup<Leisure> m_Leisure;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        private ComponentLookup<Game.Objects.Transform> m_Transforms;
        private int m_JobSeekerMilestone;
        private int m_JobSeekerFailureIncrement;
        private bool m_AcceptLowerJobs;
        private bool m_AcceptSwitchJobs;
        private bool m_ReducedDaysOff;
        private bool m_PromoteJob;
        private bool m_AcceptCloserJobsEmployed;
        private bool m_AcceptCloserJobsUnemployed;
        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }

        public void Update(ref SystemState state) {
            throw new System.NotImplementedException();
        }

        public SCJob Create(ref SystemState state, in SCCommon common) {

            var countWorkplacesSystem = state.World.GetOrCreateSystemManaged<CountWorkplacesSystem>();
            Workplaces localFreeWorkplaces = countWorkplacesSystem.GetFreeWorkplaces();

            var workplaceQuery = state.GetEntityQuery( ComponentType.ReadOnly<FreeWorkplaces>(),  ComponentType.Exclude<Deleted>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Destroyed>());

            return new SCJob {
                m_Common = common,
                
                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_AcceptLowerJobs = Mod.Settings.AcceptLowerJobs,
                m_AcceptSwitchJobs = Mod.Settings.AcceptJobSwitch,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff,
                m_PromoteJob = Mod.Settings.WorkplacePromotion,
                m_AcceptCloserJobsEmployed = Mod.Settings.CloserJobEmployed,
                m_AcceptCloserJobsUnemployed = Mod.Settings.CloserJobUnemployed,

                m_Workplaces = localFreeWorkplaces,
                m_WorkplaceEntities = workplaceQuery.ToEntityArray(state.WorldUpdateAllocator),
                m_Workers = state.GetComponentLookup<Worker>(true),
                m_FreeWorkplaces = state.GetComponentLookup<FreeWorkplaces>(true),
                m_CompanyDatas = state.GetComponentLookup<CompanyData>(true),
                m_TravelPurposes = state.GetComponentLookup<TravelPurpose>(true),
                m_Leisure = state.GetComponentLookup<Leisure>(true),
                m_PropertyRenters = state.GetComponentLookup<PropertyRenter>(true),
                m_Transforms = state.GetComponentLookup<Game.Objects.Transform>(true),                
            };
        }

        public SCJob Create() {
            return new SCJob {
                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_ReducedDaysOff = Mod.Settings.ReducedDaysOff,
            };
        }

        public bool StartJobSeek(Entity citizen, SmallCityJobsPhase phase) {

            if (!m_Common.HasSmallCityBehaviour(citizen)) {
                return false;
            }

            CmpSmallCityJobs cmp = m_Common.GetSmallCitiyJobs(citizen);
            cmp.m_FoundCloserJob = false;
            cmp.m_FoundHigherJob = false;
            cmp.m_phase = phase;
            m_Common.SetCmp(citizen, cmp);

            return true;
        }

        public bool IsSmallCityBehavior(Entity citizen) {
            return m_Common.IsSmallCityBehaviour(citizen);
        }

        public int GetFreeWorkplaces(int level) {
            return m_Workplaces[level];
        }

        public bool isEmployed(Entity citizen) {
            return m_Workers.HasComponent(citizen);
        }
        public bool IsIdle(Entity citizen) {
            return !m_TravelPurposes.HasComponent(citizen);
        }
        public bool IsCompany(Entity workplace) {
            return m_CompanyDatas.HasComponent(workplace);
        }

        public bool FoundCloserJob(Entity citizen) {
            return m_Common.HasSmallCitiyJobs(citizen) && m_Common.GetSmallCitiyJobs(citizen).m_FoundCloserJob;
        }

        public bool FoundHigherJob(Entity citizen) {
            return m_Common.HasSmallCitiyJobs(citizen) && m_Common.GetSmallCitiyJobs(citizen).m_FoundHigherJob;
        }

        public bool HasClosestJob(Entity citizen, int currentJobLevel) {
            return GetClosestJob(citizen, currentJobLevel) != Entity.Null;
        }

        public Entity GetClosestJob(Entity citizen, int currentJobLevel) {
            if (isEmployed(citizen)) return GetClosestSameLevelJobEmployed(citizen, currentJobLevel);
            return GetClosestJobUnemployed(citizen, m_Common.EducationLevel(citizen));
        }

        public Entity GetClosestJobUnemployed(Entity citizen, int educationLevel) {
            if (!m_Common.GetHouseholdMembers().HasComponent(citizen))
                return Entity.Null;

            Entity household = m_Common.GetHouseholdMembers()[citizen].m_Household;

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

        public Entity GetClosestSameLevelJobEmployed(Entity citizen, int currentJobLevel) {
            if (currentJobLevel < 0 || !m_Workers.HasComponent(citizen) || !m_Common.GetHouseholdMembers().HasComponent(citizen)) {
                return Entity.Null;
            }

            Worker worker = m_Workers[citizen];
            Entity household = m_Common.GetHouseholdMembers()[citizen].m_Household;

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

        public bool AcceptLowerJobs() {
            return m_AcceptLowerJobs;
        }

        public bool IsWorkplaceAllowed(Entity owner, Entity destination, ComponentLookup<PrefabRef> prefabs) {
            if (!prefabs.HasComponent(destination))
                return false;
            bool sameWorkplace = m_Workers.HasComponent(owner) && destination == m_Workers[owner].m_Workplace;
            if (sameWorkplace && m_PromoteJob) return true;

            return !sameWorkplace;
        }

        public bool IsPromotionAllowed(Entity owner, Entity destination, ComponentLookup<PrefabRef> prefabs, int bestFor) {
            bool sameWorkplace = m_Workers.HasComponent(owner) && destination == m_Workers[owner].m_Workplace;

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

                    m_Common.Send(owner, CustomEventType.PromotedJob);
                    return;
                }
            }
        }

        public bool UnemployedSkippedApplication(int numJobs, Entity citizen) {
            if (numJobs <= 0) {
                m_Common.Send(citizen, CustomEventType.NoJobsAvailable);
                return true;
            }

            bool useSmallCityBehavior = m_Common.IsSmallCityBehaviour(citizen);
            bool applicationFailed =
                numJobs < SCCommon.GetRandom(ref m_Common).NextInt(VanillaWorkspaceThreshold);
            bool failed = applicationFailed && !useSmallCityBehavior;

            if (!failed && useSmallCityBehavior && m_AcceptCloserJobsUnemployed) {
                CmpSmallCityJobs cmp = m_Common.GetSmallCitiyJobs(citizen);
                cmp.m_FoundCloserJob = true;
                cmp.m_FoundHigherJob = false;
                cmp.m_UseSmallCityBehaviour = useSmallCityBehavior;
                cmp.m_phase = m_Common.GetPhase();
                m_Common.SetCmp(citizen, cmp);
            } else if (failed) {
                m_Common.Send(citizen, CustomEventType.DoesntLikeAnyJobs);
            }

            return failed;
        }

        public bool EmployedSkippedApplication(int numJobs, int totalAvailableJobs, int worseJobs,
            int currentJobLevel, int highestAvailableJobLevel, Entity citizen) {

            if (!IsIdle(citizen) || m_Leisure.HasComponent(citizen)) return true;

            if (numJobs <= 0) {
                if (totalAvailableJobs <= 0)
                    m_Common.Send(citizen, CustomEventType.SwitchJobNone);
                else if (worseJobs == totalAvailableJobs)
                    m_Common.Send(citizen, CustomEventType.SwitchJobWorse);
                else
                    m_Common.Send(citizen, CustomEventType.SwitchJobNoTake);

                return true;
            }

            bool useSmallCityBehavior = m_Common.IsSmallCityBehaviour(citizen);
            bool vanillaSkipped = numJobs <= VanillaWorkspaceThreshold || numJobs < SCCommon.GetRandom(ref m_Common).NextInt(VanillaSwitchDesireThreshold);

            if (!m_AcceptSwitchJobs)
                return vanillaSkipped;

            bool hasBetterJob = highestAvailableJobLevel > currentJobLevel;
            bool hasCloserJob = useSmallCityBehavior && m_AcceptCloserJobsEmployed && HasClosestJob(citizen, currentJobLevel);

            if (useSmallCityBehavior) {
                CmpSmallCityJobs cmp = m_Common.GetSmallCitiyJobs(citizen);
                cmp.m_FoundHigherJob = hasBetterJob;
                cmp.m_FoundCloserJob = hasCloserJob;
                cmp.m_UseSmallCityBehaviour = useSmallCityBehavior;
                cmp.m_phase = m_Common.GetPhase();
                m_Common.SetCmp(citizen, cmp);
                if (hasCloserJob) m_Common.Send(citizen, CustomEventType.FoundCloserJob);
            }

            if (hasBetterJob && vanillaSkipped && !useSmallCityBehavior) {
                if (numJobs <= VanillaWorkspaceThreshold) {
                    m_Common.AddParameter(numJobs);
                    m_Common.SetHint(EventHint.IgnoreParameters);
                    m_Common.Send(citizen, CustomEventType.TooFewBetterJobs);
                } else {
                    m_Common.AddParameter(numJobs);
                    m_Common.Send(citizen, CustomEventType.StartedLookingForAnotherJob);
                    m_Common.Send(citizen, CustomEventType.DoesntWantBetterJob);
                }
            }
            bool noBetterOrCloserJobs = !hasBetterJob && !hasCloserJob;

            if (noBetterOrCloserJobs)
                m_Common.Send(citizen, CustomEventType.NoSuitableSwitch);

            if (!useSmallCityBehavior)
                return vanillaSkipped;

            return noBetterOrCloserJobs;
        }

        public void PrintGoneIssues(Entity citizenEntity, Entity workplace) {
            if (m_Common.IsFollowed(citizenEntity)) {
                if (IsCompany(workplace)) {
                    if (!m_PropertyRenters.TryGetComponent(workplace, out PropertyRenter renter) ||
                        renter.m_Property == Entity.Null) {
                        m_Common.SetWatchHint(EventHint.WatchEvent);
                        m_Common.Send(citizenEntity, CustomEventType.EmployerGone);
                    } else {
                        m_Common.SetHint(EventHint.WatchEvent);
                        m_Common.SetEventToWatch(CustomEventType.EmployerGone);
                        m_Common.Send(citizenEntity, CustomEventType.EmployerReturned);
                    }
                } else if (!m_Common.HasBuilding(workplace) &&
                           !m_Common.HasOutsideConnection(workplace)) {
                    m_Common.Send(citizenEntity, CustomEventType.WorkplaceGone);
                }
            }
        }
        public void PrintLookingForWork(Entity citizenEntity, int num, int matchingEducationPositions) {
            m_Common.AddParameter(num);
            m_Common.AddParameter(matchingEducationPositions);
            m_Common.Send(citizenEntity, CustomEventType.StartedLookingForWork);
        }
        public void PrintLookingForAnotherWork(Entity citizenEntity, int num) {
            m_Common.AddParameter(num);
            m_Common.Send(citizenEntity, CustomEventType.StartedLookingForAnotherJob);
        }
    }
}
