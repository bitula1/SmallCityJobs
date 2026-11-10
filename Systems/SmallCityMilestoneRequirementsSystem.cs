using BitulaMod.SmallCity;
using Game.Prefabs;
using Game;
using Unity.Entities;
using Game.Simulation;
using Unity.Collections;
using System;

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

                    done &= milestones.Get().MoveRequirementTo("Map Tiles", "Tiny Hamlet", "Milestone 1");

                    //Budget
                    done &= milestones.Get().RemoveUnlockRequirements("City Budget");
                    done &= milestones.Get().RemoveUnlockRequirements("Taxation");

                    //Death Care
                    done &= milestones.Get().MoveRequirementTo("Deathcare", "Hamlet", "Health & Deathcare");
                    done &= milestones.Get().MoveRequirementTo("Cemetery02", "Hamlet", "Health & Deathcare");
                    done &= milestones.Get().MoveRequirementTo("Cemetery02", "Hamlet", "BasicHealthcareNode");
                    
                    // City management
                    done &= milestones.Get().MoveRequirementTo("City Statistics", "Tiny Hamlet", "Milestone 1");
                    done &= milestones.Get().MoveRequirementTo("Service Budgets", "Tiny Hamlet", "Milestone 1");

                    // Residential zoning
                    //done &= milestones.Get().MoveRequirementTo("EU Residential Medium Row", "Tiny Hamlet", "Milestone 1", m_PrefabSystem);
                    //done &= milestones.Get().MoveRequirementTo("NA Residential Medium Row", "Tiny Hamlet", "Milestone 1", m_PrefabSystem);

                    // Specialized industry
                    done &= milestones.Get().MoveRequirementTo("Agriculture Area Placeholder - Vegetable", "Tiny Hamlet", "Milestone 1");
                    done &= milestones.Get().LockRequirementTo("Small Hamlet", "Agriculture Area Placeholder - Grain");
                    done &= milestones.Get().LockRequirementTo("Hamlet", "Forestry Area Placeholder");
                    done &= milestones.Get().LockRequirementTo("Large Hamlet", "Ore Area Placeholder - Stone");
                    done &= milestones.Get().LockRequirementTo("Milestone 1", "Aquaculture Area Placeholder - Land");

                    //Parks and recreation
                    done &= milestones.Get().AddUnlockRequirementToAll("Milestone 4", "Parks & Recreation");
                    done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark01");
                    done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark02");
                    done &= milestones.Get().UnlockRequirementTo("Milestone 4", "PocketPark04");
                    done &= milestones.Get().MoveRequirementTo("Parks & Recreation", "Tiny Hamlet", "Milestone 4");
                    done &= milestones.Get().MoveRequirementTo("PocketPark01", "Tiny Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo("PocketPark01", "Tiny Hamlet", "BasicParks&RecreationNode");
                    done &= milestones.Get().MoveRequirementTo("PocketPark02", "Tiny Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo("PocketPark02", "Tiny Hamlet", "BasicParks&RecreationNode");
                    done &= milestones.Get().MoveRequirementTo("PocketPark04", "Small Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo("PocketPark04", "Small Hamlet", "BasicParks&RecreationNode");

                    //Electricity
                    done &= milestones.Get().LockRequirementTo("Tiny Hamlet", "Electricity");
                    done &= milestones.Get().AddUnlockRequirementToAll("Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("WindTurbine03", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("TransformerStation02", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("WindTurbine02", "Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("WindTurbine01", "Large Hamlet", "Milestone 1", "BasicElectricityNode");


                    //Water & Sewage
                    done &= milestones.Get().LockRequirementTo("Small Hamlet", "Water & Sewage");
                    done &= milestones.Get().AddUnlockRequirementToAll("Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("WaterTower03", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo("SewageOutlet01", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");

                    done &= milestones.Get().LockInfoviewTo("Tiny Hamlet", "Electricity", m_PrefabSystem);
                    using (var ecb = new EntityCommandBuffer(Allocator.Temp)) {
                        done &= milestones.Get().LockMenuTo("Tiny Hamlet", "Electricity", m_PrefabSystem, ecb);
                        done &= milestones.Get().LockMenuTo("Tiny Hamlet", "Water & Sewage", m_PrefabSystem, ecb);
                        done &= milestones.Get().RemoveMenuUnlock("WaterTutorialIntro", "Water & Sewage", m_PrefabSystem, ecb);
                        ecb.Playback(EntityManager);
                    }

                    if (!done)
                        Mod.log.Error("Milestone initialization incomplete");
                    LogEconomyUnlockRequirements();
                } finally {
                    m_Done = true;
                }
            World.GetExistingSystemManaged<DispatchElectricitySystem>().Enabled = milestones.GetMilestoneInfo().IsElectricityEnabled();
            World.GetExistingSystemManaged<DispatchWaterSystem>().Enabled = milestones.GetMilestoneInfo().IsWaterEnabled();
        }

        private void LogEconomyUnlockRequirements() {
            var prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PrefabData>(),
                ComponentType.ReadOnly<UnlockRequirement>()
            );

            using (var entities = query.ToEntityArray(Allocator.Temp)) {
                foreach (var entity in entities) {
                    var prefab = prefabSystem.GetPrefab<PrefabBase>(entity);

                    if (prefab == null)
                        continue;

                    string name = prefab.name;

                    if (name.IndexOf("Budget", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Service", StringComparison.OrdinalIgnoreCase) < 0 &&
                        name.IndexOf("Tax", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var requirements = EntityManager.GetBuffer<UnlockRequirement>(entity);

                    Mod.log.Info($"Prefab {name} Requirements {requirements.Length}");

                    foreach (var requirement in requirements) {
                        var requiredPrefab = prefabSystem.GetPrefab<PrefabBase>(
                            requirement.m_Prefab);

                        Mod.log.Info(
                            $"  Requirement {requiredPrefab?.name ?? "UNKNOWN"} Flags {requirement.m_Flags}");
                    }
                }
            }
        }

    }
}