using Game.Citizens;
using Unity.Collections;
using Unity.Entities;


namespace BitulaMod.SmallCity {
    public struct SCWorkerInfo : ISCInfo<SCWorkerInfo> {
        [ReadOnly]
        private ComponentLookup<Worker> m_Workers;

        public SCWorkerInfo Create(ref SystemState state) {

            return new SCWorkerInfo {
                m_Workers = state.GetComponentLookup<Worker>(true),
            };
        }

        public SCWorkerInfo Create() {
            return new SCWorkerInfo {
            };
        }

        public bool isEmployed(Entity citizen) {
            return m_Workers.HasComponent(citizen);
        }

        public Worker GetWorker(Entity citizenEntity) {            
            return isEmployed(citizenEntity) ? m_Workers[citizenEntity] : default;
        }

        public void Update(ref SystemState state) {
            m_Workers.Update(ref state);
        }
    }
}
