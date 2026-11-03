using BitulaMod.SmallCity;
using Game.Prefabs;
using Game;
using Unity.Entities;
using Game.UI.InGame;

namespace BitulaMod.Systems {
    public partial class SmallCityMilestoneRequirementsSystem : GameSystemBase {
        private PrefabSystem m_PrefabSystem;
        private bool m_Done;

        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        protected override void OnUpdate() {
            if (m_Done)
                return;
            bool done = true;
            try {
                var milestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef);
                
                done &= milestones.Get().MoveRequirementTo("Deathcare", "Hamlet", "Health & Deathcare", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Cemetery02", "Hamlet", "Health & Deathcare", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Cemetery02", "Hamlet", "BasicHealthcareNode", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Map Tiles", "Hamlet", "Milestone 1", m_PrefabSystem);

                // City management
                done &= milestones.Get().MoveRequirementTo("City Budget", "Hamlet", "Milestone 1", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Taxation", "Hamlet", "Milestone 1", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("City Statistics", "Hamlet", "Milestone 1", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Service Budgets", "Hamlet", "Milestone 1", m_PrefabSystem);

                // Residential zoning
                done &= milestones.Get().MoveRequirementTo("EU Residential Medium Row", "Hamlet", "Milestone 1", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("NA Residential Medium Row", "Hamlet", "Milestone 1", m_PrefabSystem);

                // Specialized industry
                done &= milestones.Get().MoveRequirementTo("Agriculture Area Placeholder - Vegetable", "Hamlet", "Milestone 1", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Ore Area Placeholder - Coal", "Hamlet", "Milestone 1", m_PrefabSystem);

                //Parks and recreation
                milestones.Get().LockRequirementToAll("Milestone 4", "Parks & Recreation", m_PrefabSystem);
                done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark01", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark01", "Hamlet", "Parks & Recreation", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark01", "Hamlet", "BasicParks&RecreationNode", m_PrefabSystem);
                done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark02", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark02", "Hamlet", "Parks & Recreation", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark02", "Hamlet", "BasicParks&RecreationNode", m_PrefabSystem);
                done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark04", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark04", "Hamlet", "Parks & Recreation", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("PocketPark04", "Hamlet", "BasicParks&RecreationNode", m_PrefabSystem);
                done &= milestones.Get().MoveRequirementTo("Parks & Recreation", "Hamlet", "Milestone 4", m_PrefabSystem);

                if (!done)
                    Mod.log.Error("Milestone initialization incomplete");
            } finally {
                m_Done = true;                
            }




            
        }
    }
}