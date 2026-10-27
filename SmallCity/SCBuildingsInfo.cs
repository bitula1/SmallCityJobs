using Game.Buildings;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCBuildingsInfo : ISCInfo<SCBuildingsInfo> {
        [ReadOnly]
        private ComponentLookup<PropertyRenter> m_PropertyRenters;

        public SCBuildingsInfo Create(ref SystemState state) {
            return new SCBuildingsInfo {
                m_PropertyRenters = state.GetComponentLookup<PropertyRenter>(true),
            };
        }

        public bool HasPropertyRenter(Entity entity) {
            return m_PropertyRenters.HasComponent(entity);
        }

        public PropertyRenter GetPropertyRenter(Entity entity) {
            return HasPropertyRenter(entity) ? m_PropertyRenters[entity] : default;
        }

        public Entity GetBuilding(Entity entity) {
            if (m_PropertyRenters.TryGetComponent(entity, out PropertyRenter renter) &&
                renter.m_Property != Entity.Null) {
                return renter.m_Property;
            }
            return entity;
        }

        public void Update(ref SystemState state) {
            m_PropertyRenters.Update(ref state);
        }
    }
}