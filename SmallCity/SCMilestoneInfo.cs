using Game.Prefabs;
using Unity.Entities;
using System.Collections.Generic;
using BitulaMod.Components;
using Unity.Collections;
using Game.City;

namespace BitulaMod.SmallCity {
    public struct SCMilestoneInfo : ISCInfo<SCMilestoneInfo> {
        private ComponentLookup<MilestoneData> m_Milestones;
        private SCInfo<SCPrefabInfo> m_PrefabInfo;
        private EntityQuery m_MilestoneQuery;
        private EntityQuery m_CustomMilestoneQuery;
        private EntityQuery m_MilestoneLevelQuery;

        public SCMilestoneInfo Create(ref SystemState state) {
            return new SCMilestoneInfo {
                m_Milestones = state.GetComponentLookup<MilestoneData>(false),
                m_PrefabInfo = SCInfo<SCPrefabInfo>.Create(ref state),
                m_MilestoneQuery = state.GetEntityQuery(new EntityQueryDesc {
                    All = new[] {
                        ComponentType.ReadOnly<MilestoneData>(),
                        ComponentType.ReadOnly<PrefabData>()
                    },
                    Options = EntityQueryOptions.IncludePrefab
                }),
                m_CustomMilestoneQuery = state.GetEntityQuery(new EntityQueryDesc {
                    All = new[] {
                        ComponentType.ReadOnly<MilestoneData>(),
                        ComponentType.ReadOnly<CmpCustomMilestone>()
                    },
                    Options = EntityQueryOptions.IncludePrefab
                }),
                m_MilestoneLevelQuery = state.GetEntityQuery(
                    ComponentType.ReadOnly<MilestoneLevel>())
                };
        }

        public void Update(ref SystemState state) {
            m_Milestones.Update(ref state);
            m_PrefabInfo.Update(ref state);
        }

        public bool HasMilestone(Entity entity) {
            return m_Milestones.HasComponent(entity);
        }

        public bool TryGetMilestone(Entity entity, out MilestoneData milestone) {
            return m_Milestones.TryGetComponent(entity, out milestone);
        }

        public MilestonePrefab GetMilestone(Entity entity, PrefabSystem prefabSystem) {
            return m_PrefabInfo.Get().GetPrefab<MilestonePrefab>(entity, prefabSystem);
        }

        public Entity GetEntity(MilestonePrefab milestone, PrefabSystem prefabSystem) {
            return m_PrefabInfo.Get().GetEntity(milestone, prefabSystem);
        }

        public List<(Entity entity, MilestonePrefab milestone)> GetMilestones(PrefabSystem prefabSystem) {
            var milestones = new List<(Entity entity, MilestonePrefab milestone)>();

            using (var entities = m_MilestoneQuery.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    MilestonePrefab milestone = GetMilestone(entity, prefabSystem);

                    if (milestone != null)
                        milestones.Add((entity, milestone));
                }
            }

            return milestones;
        }

        public List<(Entity entity, MilestonePrefab milestone)> GetCustomMilestones(PrefabSystem prefabSystem) {
            var milestones = new List<(Entity entity, MilestonePrefab milestone)>();

            using (var entities = m_CustomMilestoneQuery.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    MilestonePrefab milestone = GetMilestone(entity, prefabSystem);

                    if (milestone != null)
                        milestones.Add((entity, milestone));
                }
            }

            return milestones;
        }

        public bool IsDeathCareEnabled() {
            return !m_MilestoneLevelQuery.IsEmptyIgnoreFilter &&
                m_MilestoneLevelQuery.GetSingleton<MilestoneLevel>()
                    .m_AchievedMilestone >= 1;
        }
    }
}