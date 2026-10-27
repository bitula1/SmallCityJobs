using Game.Citizens;
using Unity.Collections;
using Unity.Entities;


namespace BitulaMod.SmallCity {
    public struct SCStudentInfo : ISCInfo<SCStudentInfo> {
        [ReadOnly]
        private ComponentLookup<Student> m_Student;

        public SCStudentInfo Create(ref SystemState state) {

            return new SCStudentInfo {
                m_Student = state.GetComponentLookup<Student>(true),
            };
        }

        public bool isStudent(Entity citizen) {
            return m_Student.HasComponent(citizen);
        }

        public Student GetStudent(Entity citizenEntity) {
            return isStudent(citizenEntity) ? m_Student[citizenEntity] : default;
        }

        public void Update(ref SystemState state) {
            m_Student.Update(ref state);
        }
    }
}
