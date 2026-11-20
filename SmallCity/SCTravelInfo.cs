using Game.Citizens;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCTravelInfo : ISCInfo<SCTravelInfo> {
        [ReadOnly]
        private ComponentLookup<TravelPurpose> m_TravelPurposes;
        private ComponentLookup<AttendingMeeting> m_AttendingMeetings;

        public SCTravelInfo Create(ref SystemState state) {
            return new SCTravelInfo {
                m_TravelPurposes = state.GetComponentLookup<TravelPurpose>(true),
                m_AttendingMeetings = state.GetComponentLookup<AttendingMeeting>(true),
            };
        }

        public void Update(ref SystemState state) {
            m_TravelPurposes.Update(ref state);
            m_AttendingMeetings.Update(ref state);
        }

        public bool HasMeeting(Entity citizen) {
            return m_AttendingMeetings.HasComponent(citizen);
        }

        public bool HasPurpose(Entity citizen, Purpose purpose) {
            return m_TravelPurposes.TryGetComponent(citizen, out TravelPurpose travelPurpose) &&
                travelPurpose.m_Purpose == purpose;
        }

        public bool HasPurpose(Entity citizen) {
            return m_TravelPurposes.HasComponent(citizen);
        }

        public bool TryGetPurpose(Entity citizen, Purpose purpose, out TravelPurpose travelPurpose) {
            return m_TravelPurposes.TryGetComponent(citizen, out travelPurpose) &&
                travelPurpose.m_Purpose == purpose;
        }
    }
}