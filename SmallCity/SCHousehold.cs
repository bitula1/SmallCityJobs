using Game.Citizens;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCHouseholdInfo : ISCInfo<SCHouseholdInfo> {
        [ReadOnly]
        private ComponentLookup<HouseholdMember> m_HouseholdMembers;

        [ReadOnly]
        private BufferLookup<Game.Economy.Resources> m_Resources;

        public SCHouseholdInfo Create(ref SystemState state) {
            return new SCHouseholdInfo {
                m_HouseholdMembers = state.GetComponentLookup<HouseholdMember>(true),
                m_Resources = state.GetBufferLookup<Game.Economy.Resources>(true)
            };
        }

        public int GetMoney(Entity citizen) {
            if (m_HouseholdMembers.TryGetComponent(citizen, out HouseholdMember member) &&
                m_Resources.HasBuffer(member.m_Household))
                return EconomyUtils.GetResources(Resource.Money, m_Resources[member.m_Household]);

            return 0;
        }

        public void Update(ref SystemState state) {
            m_HouseholdMembers.Update(ref state);
            m_Resources.Update(ref state);
        }
    }
}