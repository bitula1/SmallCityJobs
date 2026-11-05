using BitulaMod.SmallCity;
using Game.Prefabs;
using Game;
using Unity.Entities;
using Game.Simulation;
using Unity.Collections;

namespace BitulaMod.Systems {
    public partial class SmallCityMilestoneRequirementsSystem : GameSystemBase {
        private PrefabSystem m_PrefabSystem;
        private bool m_Done;
        private bool m_MenuDebugLogged;

        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        protected override void OnUpdate() {
            bool done = true;
            var milestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef);
            if (!m_Done)
                try {

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
                    done &= milestones.Get().LockRequirementToAll("Milestone 4", "Parks & Recreation", m_PrefabSystem);
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

                    //Electricity
                    done &= milestones.Get().LockRequirementTo("Hamlet", "Electricity", m_PrefabSystem);
                    done &= milestones.Get().LockRequirementToAll("Milestone 1", "BasicElectricityNode", m_PrefabSystem);
                    done &= milestones.Get().UnlockRequirementTo("Milestone 1", "WindTurbine03", m_PrefabSystem);
                    done &= milestones.Get().MoveRequirementTo("WindTurbine03", "Hamlet", "BasicElectricityNode", m_PrefabSystem);

                    //Water & Sewage
                    done &= milestones.Get().LockRequirementTo("Hamlet", "Water & Sewage", m_PrefabSystem);
                    done &= milestones.Get().LockRequirementToAll("Milestone 1", "BasicWater&SewageNode", m_PrefabSystem);
                    done &= milestones.Get().UnlockRequirementTo("Milestone 1", "WaterTower03", m_PrefabSystem);
                    done &= milestones.Get().MoveRequirementTo("WaterTower03", "Hamlet", "BasicWater&SewageNode", m_PrefabSystem);
                    done &= milestones.Get().UnlockRequirementTo("Milestone 1", "SewageOutlet01", m_PrefabSystem);
                    done &= milestones.Get().MoveRequirementTo("SewageOutlet01", "Hamlet", "BasicWater&SewageNode", m_PrefabSystem);

                    done &= milestones.Get().LockInfoviewTo("Hamlet", "Electricity", m_PrefabSystem);
                    using (var ecb = new EntityCommandBuffer(Allocator.Temp)) {
                        done &= milestones.Get().LockMenuTo("Hamlet", "Electricity", m_PrefabSystem, ecb);
                        done &= milestones.Get().LockMenuTo("Hamlet", "Water & Sewage", m_PrefabSystem, ecb);
                        done &= milestones.Get().RemoveMenuUnlock("WaterTutorialIntro", "Water & Sewage", m_PrefabSystem, ecb);
                        ecb.Playback(EntityManager);
                    }

                    if (!done)
                        Mod.log.Error("Milestone initialization incomplete");


                } finally {
                    m_Done = true;
                }
            World.GetExistingSystemManaged<DispatchElectricitySystem>().Enabled = milestones.GetMilestoneInfo().IsElectricityEnabled();
            World.GetExistingSystemManaged<DispatchWaterSystem>().Enabled = milestones.GetMilestoneInfo().IsWaterEnabled();
        }
    }
}