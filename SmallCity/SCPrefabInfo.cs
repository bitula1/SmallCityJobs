using Game.Prefabs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCPrefabInfo : ISCInfo<SCPrefabInfo> {
        [ReadOnly]
        private ComponentLookup<PrefabData> m_Prefabs;
        private EntityQuery m_AllPrefabsQuery;

        public SCPrefabInfo Create(ref SystemState state) {
            return new SCPrefabInfo {
                m_Prefabs = state.GetComponentLookup<PrefabData>(true),
                m_AllPrefabsQuery = state.GetEntityQuery(new EntityQueryDesc {
                    All = new[] {
                        ComponentType.ReadOnly<PrefabData>()
                    },
                    Options = EntityQueryOptions.IncludePrefab
                }),
            };
        }

        public void Update(ref SystemState state) {
            m_Prefabs.Update(ref state);
        }

        public bool HasPrefab(Entity entity) {
            return m_Prefabs.HasComponent(entity);
        }

        public bool TryGetPrefabData(Entity entity, out PrefabData prefabData) {
            return m_Prefabs.TryGetComponent(entity, out prefabData);
        }

        public T GetPrefab<T>(Entity entity, PrefabSystem prefabSystem) where T : PrefabBase {
            return m_Prefabs.TryGetComponent(entity, out PrefabData prefabData)
                ? prefabSystem.GetPrefab<T>(prefabData)
                : null;
        }

        public Entity GetEntity(string name, PrefabSystem prefabSystem) {
            foreach (var prefab in GetAllPrefabs<PrefabBase>(prefabSystem)) {
                if (prefab != null && prefab.name == name)
                    return GetEntity(prefab, prefabSystem);
            }

            throw new System.ArgumentException($"Prefab not found: {name}");
        }

        public List<T> GetPrefabs<T>(EntityQuery query, PrefabSystem prefabSystem) where T : PrefabBase {
            using (var prefabs = query.ToComponentDataArray<PrefabData>(Allocator.Temp)) {
                var result = new List<T>(prefabs.Length);

                foreach (var prefabData in prefabs) {
                    result.Add(prefabSystem.GetPrefab<T>(prefabData));
                }

                return result;
            }
        }
        public List<T> GetAllPrefabs<T>(PrefabSystem prefabSystem) where T : PrefabBase {
            return GetPrefabs<T>(m_AllPrefabsQuery, prefabSystem);
        }


        public Entity GetEntity(PrefabBase prefab, PrefabSystem prefabSystem) {
            return prefabSystem.GetEntity(prefab);
        }
    }
}