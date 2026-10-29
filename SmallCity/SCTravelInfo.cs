using Game.Citizens;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCTravelInfo : ISCInfo<SCTravelInfo> {
        [ReadOnly]
        private ComponentLookup<TravelPurpose> m_TravelPurposes;

        public SCTravelInfo Create(ref SystemState state) {
            return new SCTravelInfo {
                m_TravelPurposes = state.GetComponentLookup<TravelPurpose>(true)
            };
        }

        public void Update(ref SystemState state) {
            m_TravelPurposes.Update(ref state);
        }

        public bool HasPurpose(Entity citizen, Purpose purpose) {
            return m_TravelPurposes.TryGetComponent(citizen, out TravelPurpose travelPurpose) &&
                travelPurpose.m_Purpose == purpose;
        }

        public bool TryGetPurpose(Entity citizen, Purpose purpose, out TravelPurpose travelPurpose) {
            return m_TravelPurposes.TryGetComponent(citizen, out travelPurpose) &&
                travelPurpose.m_Purpose == purpose;
        }
    }
}