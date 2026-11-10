using BitulaMod.Components;
using BitulaMod.SmallCity;
using Colossal.Entities;
using Game;
using Game.Prefabs;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace BitulaMod.Systems {
    public partial class SmallCityMilestoneSystem : GameSystemBase {
        private SmallCity<SCMilestones> m_SmallCityMilestones;
        private PrefabSystem m_PrefabSystem;
        private bool m_Done = false;

        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            Mod.log.Info("SmallCityMilestoneSystem created");
        }

        protected override void OnUpdate() {
            if (!m_Done) {
                try {
                    CustomMilestone cm;
                    m_SmallCityMilestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef);
                    addMilestone(CustomMilestone.MakeDefault("Tiny Hamlet", 750, 1));
                    addMilestone(CustomMilestone.MakeDefault("Small Hamlet", 1200, 2));
                    addMilestone(CustomMilestone.MakeDefault("Hamlet", 2000, 3));                    
                    cm = CustomMilestone.MakeDefault("Large Hamlet", 3600, 4);
                    cm.Major = true;
                    addMilestone(cm);
                    var milestones = m_SmallCityMilestones.GetMilestoneInfo().GetMilestones(m_PrefabSystem);
                    m_SmallCityMilestones.Get().AddMapTilePoints(milestones, "Milestone 1", -2);
                    m_SmallCityMilestones.Get().AddMapTilePoints(milestones, "Milestone 2", -2);
                    var allMilestones = m_SmallCityMilestones.GetMilestoneInfo().GetMilestones(m_PrefabSystem);
                    var customMilestones = m_SmallCityMilestones.GetMilestoneInfo().GetCustomMilestones(m_PrefabSystem);
                    m_SmallCityMilestones.Get().UpdateMajorMilestones(allMilestones, customMilestones);
                } finally {
                    m_Done = true;
                }

            }            
        }

        private void addMilestone(CustomMilestone cm) {
            var milestones = m_SmallCityMilestones.GetMilestoneInfo().GetMilestones(m_PrefabSystem);
            MilestonePrefab prefab = m_SmallCityMilestones.Get().AddMilestone(cm, m_PrefabSystem);
            Entity entity = m_SmallCityMilestones.GetMilestoneInfo().GetEntity(prefab, m_PrefabSystem);
            EntityManager.AddComponent<CmpCustomMilestone>(entity);
            m_SmallCityMilestones.Get().MoveMilestones(milestones, cm.Index, cm.XpRequired);
        }

    }
}
