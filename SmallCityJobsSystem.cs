using Game;
using Game.Citizens;
using Game.City;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod {
    public partial class SmallCityJobsSystem : GameSystemBase {
        private Unity.Mathematics.Random m_Random;
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 16;
        }
        private EntityQuery m_NewCitizenQuery;
        private EntityQuery m_AllCitizenQuery;
        private int m_LastProgression = -1;

        protected override void OnCreate() {
            base.OnCreate();

            m_NewCitizenQuery = GetEntityQuery(
                ComponentType.ReadOnly<Citizen>(),
                ComponentType.Exclude<SmallCityJobsComponent>()
            );

            m_AllCitizenQuery = GetEntityQuery(
                ComponentType.ReadOnly<Citizen>(),
                ComponentType.ReadWrite<SmallCityJobsComponent>()
            );

            uint seed = (uint)System.Environment.TickCount;
            if (seed == 0)
                seed = 1;

            m_Random = new Unity.Mathematics.Random(seed);

            RequireForUpdate<Population>();
        }

        protected override void OnDestroy() {
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            int population = SystemAPI.GetSingleton<Population>().m_Population;
            int progression = SmallCityJobs.GetProgression(population);

            NativeArray<Entity> newCitizens =
                m_NewCitizenQuery.ToEntityArray(Allocator.Temp);

            EntityManager.AddComponent<SmallCityJobsComponent>(m_NewCitizenQuery);

            if (progression != m_LastProgression) {
                NativeArray<Entity> allCitizens =
                    m_AllCitizenQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity citizen in allCitizens) {
                    SmallCityJobsComponent cmp =
                        EntityManager.GetComponentData<SmallCityJobsComponent>(citizen);

                    cmp.m_UseSmallCityBehaviour =
                        m_Random.NextInt(100) >= progression;

                    EntityManager.SetComponentData(citizen, cmp);
                }

                allCitizens.Dispose();
                m_LastProgression = progression;
            } else {
                foreach (Entity citizen in newCitizens) {
                    SmallCityJobsComponent cmp =
                        EntityManager.GetComponentData<SmallCityJobsComponent>(citizen);

                    cmp.m_UseSmallCityBehaviour =
                        m_Random.NextInt(100) >= progression;

                    EntityManager.SetComponentData(citizen, cmp);
                }
            }

            newCitizens.Dispose();
        }
    }
}
