using Game;
using Game.Citizens;
using Unity.Entities;

namespace BitulaMod {
    public partial class SmallCityJobsSystem : GameSystemBase {
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 16;
        }
        private EntityQuery m_CitizenQuery;
        protected override void OnCreate() {
            base.OnCreate();
            m_CitizenQuery = GetEntityQuery(
                ComponentType.ReadOnly<Citizen>(),
                ComponentType.Exclude<SmallCityJobsComponent>()
            );

            RequireForUpdate(m_CitizenQuery);
        }

        protected override void OnDestroy() {
            base.OnDestroy();
        }

        protected override void OnUpdate() {
            EntityManager.AddComponent<SmallCityJobsComponent>(m_CitizenQuery);
        }
    }
}
