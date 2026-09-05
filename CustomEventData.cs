using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Companies;
using Game.Objects;
using Game.UI.Menu;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BitulaMod
{
    public struct CustomEventData
    {
        private int m_JobSeekerMilestone;
        private int m_JobSeekerFailureIncrement;
        private bool m_AcceptLowerJobs;
        private Entity m_City;
        private FixedString64Bytes m_Parameters;
        private ComponentLookup<Population> m_Population;
        private ComponentLookup<Followed> m_Followed;
        private ComponentLookup<Building> m_Buildings;
        private ComponentLookup<CompanyData> m_CompanyDatas;

        private NativeQueue<CustomEvent>.ParallelWriter m_CustomEventQueue;

        public static CustomEventData Create(ref SystemState state) {
            var cityQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Population>()
            );

            var eventSender =
                state.World.GetOrCreateSystemManaged<LifePathEventSenderSystem>();           

            return new CustomEventData {
                m_Population = state.GetComponentLookup<Population>(true),
                m_Followed = state.GetComponentLookup<Followed>(true),
                m_City = cityQuery.GetSingletonEntity(),
                m_Buildings = state.GetComponentLookup<Building>(true),
                m_CompanyDatas = state.GetComponentLookup<CompanyData>(true),
                m_JobSeekerMilestone = Mod.Settings.JobSeekerMilestone,
                m_JobSeekerFailureIncrement = Mod.Settings.JobSeekerFailureIncrement,
                m_AcceptLowerJobs = Mod.Settings.AcceptLowerJobs,
                m_CustomEventQueue = eventSender.GetQueueWriter()
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
                    m_Param = m_Parameters
                });
            }

            m_Parameters = default;
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

        public bool FailedJobApplication(int numJobs, ref Unity.Mathematics.Random random)
        {
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
    }
}
