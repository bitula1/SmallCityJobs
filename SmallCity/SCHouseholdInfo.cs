using Game.Citizens;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCHouseholdInfo : ISCInfo<SCHouseholdInfo> {
        [ReadOnly]
        private ComponentLookup<HouseholdMember> m_HouseholdMembers;
        [ReadOnly]
        private BufferLookup<HouseholdCitizen> m_HouseholdCitizens;

        [ReadOnly]
        private BufferLookup<Game.Economy.Resources> m_Resources;

        public SCHouseholdInfo Create(ref SystemState state) {
            return new SCHouseholdInfo {
                m_HouseholdMembers = state.GetComponentLookup<HouseholdMember>(true),
                m_HouseholdCitizens = state.GetBufferLookup<HouseholdCitizen>(true),
                m_Resources = state.GetBufferLookup<Game.Economy.Resources>(true),
            };
        }

        public int GetMoney(Entity citizen) {
            if (m_HouseholdMembers.TryGetComponent(citizen, out HouseholdMember member) &&
                m_Resources.HasBuffer(member.m_Household))
                return EconomyUtils.GetResources(Resource.Money, m_Resources[member.m_Household]);

            return 0;
        }
        public bool TryGetHousehold(Entity citizen, out Entity household) {
            household = Entity.Null;

            if (!m_HouseholdMembers.TryGetComponent(citizen, out HouseholdMember member))
                return false;

            household = member.m_Household;
            return household != Entity.Null;
        }
        public bool TryGetHouseholdCitizens(Entity household, out DynamicBuffer<HouseholdCitizen> citizens) {
            return m_HouseholdCitizens.TryGetBuffer(household, out citizens);
        }


        public void Update(ref SystemState state) {
            m_HouseholdMembers.Update(ref state);
            m_HouseholdCitizens.Update(ref state);
            m_Resources.Update(ref state);
        }
    }
}