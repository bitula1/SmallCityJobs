using BitulaMod.SmallCity;
using Game.Prefabs;
using Game;
using Unity.Entities;
using Game.Simulation;
using Unity.Collections;
using System;
using Game.UI.InGame;
using System.Linq;

namespace BitulaMod.Systems {
    public partial class SmallCityMilestoneRequirementsSystem : GameSystemBase {
        private PrefabSystem m_PrefabSystem;
        private bool m_Done;
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 256;
        }

        protected override void OnCreate() {
            base.OnCreate();
            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
        }

        protected override void OnUpdate() {
            bool done = true;
            var smilestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef);
            if (!m_Done)
                try {
                    using var ecb = new EntityCommandBuffer(Allocator.Temp);
                    var milestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef, ecb);

                    done &= milestones.Get().MoveRequirementTo<FeaturePrefab, MilestonePrefab>("Map Tiles", "Tiny Hamlet", "Milestone 1");

                    //Budget
                    done &= milestones.Get().RemoveUnlockRequirements<FeaturePrefab>("City Budget");
                    done &= milestones.Get().RemoveUnlockRequirements<FeaturePrefab>("Taxation");

                    done &= milestones.Get().MoveRequirementTo<FeaturePrefab, ServicePrefab>("Deathcare", "Hamlet", "Health & Deathcare");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, ServicePrefab>("Cemetery02", "Hamlet", "Health & Deathcare");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, DevTreeNodePrefab>("Cemetery02", "Hamlet", "BasicHealthcareNode");

                    // City management
                    done &= milestones.Get().MoveRequirementTo<FeaturePrefab, MilestonePrefab>("City Statistics", "Tiny Hamlet", "Milestone 1");
                    done &= milestones.Get().MoveRequirementTo<FeaturePrefab, MilestonePrefab>("Service Budgets", "Tiny Hamlet", "Milestone 1");

                    // Residential zoning
                    //done &= milestones.Get().MoveRequirementTo("EU Residential Medium Row", "Tiny Hamlet", "Milestone 1", m_PrefabSystem);
                    //done &= milestones.Get().MoveRequirementTo("NA Residential Medium Row", "Tiny Hamlet", "Milestone 1", m_PrefabSystem);

                    // Specialized industry
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, MilestonePrefab>("Agriculture Area Placeholder - Vegetable", "Tiny Hamlet", "Milestone 1");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, BuildingPrefab>("Small Hamlet", "Agriculture Area Placeholder - Grain");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, BuildingPrefab>("Hamlet", "Forestry Area Placeholder");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, BuildingPrefab>("Large Hamlet", "Ore Area Placeholder - Stone");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, BuildingPrefab>("Milestone 1", "Aquaculture Area Placeholder - Land");

                    //Parks and recreation
                    done &= milestones.Get().AddUnlockRequirementToAll<MilestonePrefab, ServicePrefab>("Milestone 4", "Parks & Recreation");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, BuildingPrefab>("Milestone 4", "PocketPark01");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, BuildingPrefab>("Milestone 4", "PocketPark02");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, BuildingPrefab>("Milestone 4", "PocketPark04");
                    done &= milestones.Get().MoveRequirementTo<ServicePrefab, MilestonePrefab>("Parks & Recreation", "Tiny Hamlet", "Milestone 4");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, ServicePrefab>("PocketPark01", "Tiny Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, DevTreeNodePrefab>("PocketPark01", "Tiny Hamlet", "BasicParks&RecreationNode");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, ServicePrefab>("PocketPark02", "Tiny Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, DevTreeNodePrefab>("PocketPark02", "Tiny Hamlet", "BasicParks&RecreationNode");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, ServicePrefab>("PocketPark04", "Small Hamlet", "Parks & Recreation");
                    done &= milestones.Get().MoveRequirementTo<BuildingPrefab, DevTreeNodePrefab>("PocketPark04", "Small Hamlet", "BasicParks&RecreationNode");

                    //Electricity
                    done &= milestones.Get().LockInfoviewTo("Tiny Hamlet", "Electricity");
                    done &= milestones.Get().LockMenuTo<MilestonePrefab>("Tiny Hamlet", "Electricity");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, ServicePrefab>("Tiny Hamlet", "Electricity");
                    done &= milestones.Get().AddUnlockRequirementToAll<MilestonePrefab, DevTreeNodePrefab>("Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<PowerLinePrefab, MilestonePrefab, DevTreeNodePrefab>("Low-voltage Line", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<PowerLinePrefab, MilestonePrefab, DevTreeNodePrefab>("High-voltage Line", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("WindTurbine03", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("TransformerStation02", "Tiny Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("WindTurbine02", "Hamlet", "Milestone 1", "BasicElectricityNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("WindTurbine01", "Large Hamlet", "Milestone 1", "BasicElectricityNode");


                    // Water & Sewage
                    done &= milestones.Get().LockMenuTo<MilestonePrefab>("Small Hamlet", "Water & Sewage");
                    done &= milestones.Get().RemoveMenuUnlock<TutorialBalloonPrefab>("WaterTutorialIntro", "Water & Sewage");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, ServicePrefab>("Small Hamlet", "Water & Sewage");
                    done &= milestones.Get().AddUnlockRequirementToAll<MilestonePrefab, DevTreeNodePrefab>("Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("WaterTower03", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<BuildingPrefab, MilestonePrefab, DevTreeNodePrefab>("SewageOutlet01", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<PipelinePrefab, MilestonePrefab, DevTreeNodePrefab>("Small Water Pipe", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<PipelinePrefab, MilestonePrefab, DevTreeNodePrefab>("Small Sewage Pipe", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");
                    done &= milestones.Get().MoveUnlockRequirementTo<PipelinePrefab, MilestonePrefab, DevTreeNodePrefab>("Combined Small Pipe", "Small Hamlet", "Milestone 1", "BasicWater&SewageNode");


                    // Roads
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, UIAssetCategoryPrefab>("Milestone 1", "RoadsMediumRoads");
                    done &= milestones.Get().LockRequirementTo<MilestonePrefab, UIAssetCategoryPrefab>("Milestone 1", "RoadsSmallRoads");
                    done &= milestones.Get().AddUnlockRequirementToAny<MilestonePrefab, UIAssetCategoryPrefab>("Milestone 1", "RoadsMediumRoads");
                    done &= milestones.Get().AddUnlockRequirementToAny<MilestonePrefab, UIAssetCategoryPrefab>("Milestone 1", "RoadsSmallRoads");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, RoadPrefab>("Milestone 1", "Small Road");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, RoadPrefab>("Milestone 1", "Gravel Road");
                    done &= milestones.Get().UnlockRequirementTo<FeaturePrefab, RoadPrefab>("RoadsSmallRoads", "Gravel Road");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, RoadPrefab>("Milestone 1", "Gravel Road Oneway");
                    done &= milestones.Get().UnlockRequirementTo<FeaturePrefab, RoadPrefab>("RoadsSmallRoads", "Gravel Road Oneway");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, RoadPrefab>("Milestone 1", "Alley");
                    done &= milestones.Get().UnlockRequirementTo<FeaturePrefab, RoadPrefab>("RoadsSmallRoads", "Alley");
                    done &= milestones.Get().UnlockRequirementTo<MilestonePrefab, RoadPrefab>("Milestone 1", "Alley Oneway");
                    done &= milestones.Get().UnlockRequirementTo<FeaturePrefab, RoadPrefab>("RoadsSmallRoads", "Alley Oneway");
                    done &= milestones.Get().MoveUnlockRequirementTo<RoadPrefab, FeaturePrefab, MilestonePrefab>("Small Road Oneway - 1 lane", "Small Hamlet", "RoadsSmallRoads", "Milestone 1");
                    done &= milestones.Get().MoveUnlockRequirementTo<RoadPrefab, FeaturePrefab, MilestonePrefab>("Small Road Oneway", "Small Hamlet", "RoadsSmallRoads", "Milestone 1");
                    done &= milestones.Get().MoveUnlockRequirementTo<RoadPrefab, FeaturePrefab, MilestonePrefab>("Pedestrian Street Small", "Small Hamlet", "RoadsSmallRoads", "Milestone 1");

                    //Garbage
                    done &= milestones.Get().MoveRequirementTo<ServicePrefab, MilestonePrefab>("Garbage Management", "Large Hamlet", "Milestone 1");

                    ecb.Playback(EntityManager);

                    if (!done)
                        Mod.log.Error("Milestone initialization incomplete");

                } finally {
                    m_Done = true;
                }

            World.GetExistingSystemManaged<DispatchElectricitySystem>().Enabled = smilestones.Get().IsElectricityEnabled();
            World.GetExistingSystemManaged<DispatchWaterSystem>().Enabled = smilestones.Get().IsWaterEnabled();
        }

        

    }
}