using Game.Buildings;
using Game.Citizens;
using Game.Economy;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCBuildingsInfo : ISCInfo<SCBuildingsInfo> {
        [ReadOnly]
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        [ReadOnly]
        private BufferLookup<Renter> m_Renters;
        [ReadOnly]
        private ComponentLookup<PrefabRef> m_PrefabRefs;
        [ReadOnly]
        private ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;
        [ReadOnly]
        private BufferLookup<Resources> m_Resources;
        [ReadOnly]
        private ComponentLookup<CurrentBuilding> m_CurrentBuildings;

        public SCBuildingsInfo Create(ref SystemState state) {
            return new SCBuildingsInfo {
                m_PropertyRenters = state.GetComponentLookup<PropertyRenter>(true),
                m_Renters = state.GetBufferLookup<Renter>(true),
                m_PrefabRefs = state.GetComponentLookup<PrefabRef>(true),
                m_IndustrialProcesses = state.GetComponentLookup<IndustrialProcessData>(true),
                m_Resources = state.GetBufferLookup<Resources>(true),
                m_CurrentBuildings = state.GetComponentLookup<CurrentBuilding>(true),
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

        public bool IsInBuilding(Entity citizen, Entity building) {
            return m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding currentBuilding) &&
                currentBuilding.m_CurrentBuilding == building;
        }

        public bool HasNoResource(Entity building, Resource resource) {
            if (resource == Resource.NoResource ||
                !m_Renters.TryGetBuffer(building, out DynamicBuffer<Renter> renters))
                return false;

            foreach (Renter renter in renters) {
                Entity provider = renter.m_Renter;

                if (!m_PrefabRefs.TryGetComponent(provider, out PrefabRef prefabRef))
                    continue;

                if (!m_IndustrialProcesses.TryGetComponent(
                    prefabRef.m_Prefab, out IndustrialProcessData process))
                    continue;

                if (process.m_Output.m_Resource != resource)
                    continue;

                if (m_Resources.TryGetBuffer(provider, out DynamicBuffer<Resources> resources) &&
                    EconomyUtils.GetResources(resource, resources) <= 0)
                    return true;
            }

            return false;
        }

        public bool TryGetSeller(Entity building, Resource resource, out Entity seller) {
            seller = Entity.Null;

            if (!m_Renters.TryGetBuffer(building, out DynamicBuffer<Renter> renters))
                return false;

            foreach (Renter renter in renters) {
                Entity company = renter.m_Renter;

                if (!m_PrefabRefs.TryGetComponent(company, out PrefabRef prefabRef))
                    continue;

                if (!m_IndustrialProcesses.TryGetComponent(prefabRef.m_Prefab, out IndustrialProcessData process))
                    continue;

                if ((process.m_Output.m_Resource & resource) == Resource.NoResource)
                    continue;

                if (!m_Resources.TryGetBuffer(company, out DynamicBuffer<Resources> resources))
                    continue;

                if (EconomyUtils.GetResources(resource, resources) <= 0)
                    continue;

                seller = company;
                return true;
            }

            return false;
        }

        public void Update(ref SystemState state) {
            m_PropertyRenters.Update(ref state);
            m_Renters.Update(ref state);
            m_PrefabRefs.Update(ref state);
            m_IndustrialProcesses.Update(ref state);
            m_Resources.Update(ref state);
            m_CurrentBuildings.Update(ref state);
        }
    }
}