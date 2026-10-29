using Game.Citizens;
using Game.Companies;
using Game.Economy;
using Game.Pathfind;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCResourceInfo : ISCInfo<SCResourceInfo> {
        [ReadOnly]
        private BufferLookup<Resources> m_Resources;
        [ReadOnly]
        private ComponentLookup<ResourceBought> m_ResourceBought;
        [ReadOnly]
        private ComponentLookup<ResourceBuyer> m_ResourceBuyers;
        [ReadOnly]
        private ComponentLookup<PathInformation> m_PathInformation;

        public SCResourceInfo Create(ref SystemState state) {
            return new SCResourceInfo {
                m_Resources = state.GetBufferLookup<Resources>(true),
                m_ResourceBought = state.GetComponentLookup<ResourceBought>(true),
                m_ResourceBuyers = state.GetComponentLookup<ResourceBuyer>(true),
                m_PathInformation = state.GetComponentLookup<PathInformation>(true)
            };
        }

        public void Update(ref SystemState state) {
            m_Resources.Update(ref state);
            m_ResourceBought.Update(ref state);
            m_ResourceBuyers.Update(ref state);
            m_PathInformation.Update(ref state);
        }

        public int GetResourceAmount(Entity entity, Resource resource) {
            if (!m_Resources.TryGetBuffer(entity, out DynamicBuffer<Resources> resources))
                return 0;

            return EconomyUtils.GetResources(resource, resources);
        }

        public bool HasResourceBuffer(Entity entity) {
            return m_Resources.HasBuffer(entity);
        }

        public bool TryGetResourceBought(Entity citizen, out ResourceBought bought) {
            return m_ResourceBought.TryGetComponent(citizen, out bought);
        }

        public bool IsMatchingResourceBuyer(Entity citizen, Entity household, Entity seller, Resource resource, int amount) {
            if (!m_ResourceBuyers.TryGetComponent(citizen, out ResourceBuyer buyer))
                return false;

            if (!m_PathInformation.TryGetComponent(citizen, out PathInformation path))
                return false;

            return buyer.m_Payer == household &&
                   buyer.m_ResourceNeeded == resource &&
                   buyer.m_AmountNeeded >= amount &&
                   path.m_Destination == seller;
        }
    }
}