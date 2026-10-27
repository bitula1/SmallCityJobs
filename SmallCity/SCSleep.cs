using Game.Citizens;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace BitulaMod.SmallCity {
    public struct SCSleep : ISCFeature<SCSleep> {
        private SCCommon m_Common;
        private SCInfo<SCWorkerInfo> m_WorkerInfo;
        private SCInfo<SCStudentInfo> m_StudentInfo;
        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }

        public SCSleep Create(ref SystemState state, in SCCommon common) {

            return new SCSleep {
                m_Common = common,
                m_WorkerInfo = SCInfo<SCWorkerInfo>.Create(ref state),
                m_StudentInfo = SCInfo<SCStudentInfo>.Create(ref state),
            };
        }

        public SCSleep Create() {
            return new SCSleep {
            };
        }

        public float2 GetSleepTime(Entity citizenEntity) {
            Citizen citizen = m_Common.GetCitizenData(citizenEntity);
            bool isWorker = m_WorkerInfo.Get().isEmployed(citizenEntity);
            bool isStudent = m_StudentInfo.Get().isStudent(citizenEntity);
            Worker worker = m_WorkerInfo.Get().GetWorker(citizenEntity);
            Student student = m_StudentInfo.Get().GetStudent(citizenEntity);
            EconomyParameterData economyParameters =  m_Common.GetEconomyParameterData();
            float2 sleepTime = CitizenBehaviorSystem.GetSleepTime(citizenEntity, citizen, ref economyParameters, 
                isWorker, worker, isStudent, student);
            return sleepTime;
        }

        public void Update(ref SystemState state) {
            m_Common.Update(ref state);
            m_WorkerInfo.Update(ref state);
            m_StudentInfo.Update(ref state);
        }
    }
}
