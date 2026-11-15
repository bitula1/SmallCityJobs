using Game.Prefabs;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCPrefabInfo : ISCInfo<SCPrefabInfo> {
        [ReadOnly]
        private ComponentLookup<PrefabData> m_Prefabs;

        public SCPrefabInfo Create(ref SystemState state) {
            return new SCPrefabInfo {
                m_Prefabs = state.GetComponentLookup<PrefabData>(true)
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

        public Entity GetEntity<T>(string name, PrefabSystem prefabSystem) where T : PrefabBase {

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc {
                All = new[] {
                    ComponentType.ReadOnly<PrefabData>()
                },
                Options = EntityQueryOptions.IncludePrefab
            });

            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities) {
                if (prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) &&
                    prefab is T &&
                    prefab.name == name)
                    return entity;
            }

            throw new ArgumentException($"Prefab not found: {name} Type={typeof(T).Name}");
        }

        public List<T> GetPrefabs<T>(EntityQuery query, PrefabSystem prefabSystem) where T : PrefabBase {

            var result = new List<T>();

            using (var entities = query.ToEntityArray(Allocator.Temp)) {
                foreach (var entity in entities) {

                    if (!prefabSystem.TryGetPrefab(entity, out PrefabBase prefab))
                        continue;

                    if (prefab is T typedPrefab)
                        result.Add(typedPrefab);
                }
            }

            return result;
        }
        /*public List<T> GetAllPrefabs<T>(PrefabSystem prefabSystem) where T : PrefabBase {
            return GetPrefabs<T>(m_AllPrefabsQuery, prefabSystem);
        }*/
        public NativeArray<Entity> GetEntitiesWith<T>(Allocator allocator) where T : struct {

            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            using var query = entityManager.CreateEntityQuery(new EntityQueryDesc {
                 All = new[] {
                    ComponentType.ReadOnly<PrefabData>(),
                    ComponentType.ReadOnly<T>()
                },
                Options = EntityQueryOptions.IncludePrefab
            });

            return query.ToEntityArray(allocator);
        }



        public Entity GetEntity(PrefabBase prefab, PrefabSystem prefabSystem) {
            return prefabSystem.GetEntity(prefab);
        }
    }
}