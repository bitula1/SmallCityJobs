using Game.Citizens;
using Game.Prefabs;
using Game.Simulation;
using Unity.Entities;
using Unity.Mathematics;

namespace BitulaMod.SmallCity {
    public struct SCStudy : ISCFeature<SCStudy> {
        private SCCommon m_Common;
        private SCInfo<SCStudentInfo> m_StudentInfo;

        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }

        public SCStudy Create(ref SystemState state, in SCCommon common) {
            return new SCStudy {
                m_Common = common,
                m_StudentInfo = SCInfo<SCStudentInfo>.Create(ref state),
            };
        }

        public SCStudy Create() {
            return new SCStudy {
            };
        }

        public bool IsStudent(Entity citizenEntity) {
            return m_StudentInfo.Get().isStudent(citizenEntity);
        }

        public float2 GetStudyTime(Entity citizenEntity) {
            Citizen citizen = m_Common.GetCitizenData(citizenEntity);
            Student student = m_StudentInfo.Get().GetStudent(citizenEntity);
            EconomyParameterData economyParameters = m_Common.GetEconomyParameterData();

            return StudentSystem.GetTimeToStudy(citizen, student, ref economyParameters);
        }

        public void Update(ref SystemState state) {
            m_Common.Update(ref state);
            m_StudentInfo.Update(ref state);
        }
    }
}