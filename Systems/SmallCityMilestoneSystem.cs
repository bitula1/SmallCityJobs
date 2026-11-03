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
            /*m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            m_MilestoneQuery = GetEntityQuery(
                ComponentType.ReadOnly<PrefabData>(),
                ComponentType.ReadOnly<MilestoneData>()
            );
            m_AllPrefabsQuery = GetEntityQuery(
                ComponentType.ReadOnly<PrefabData>()
            );*/
            Mod.log.Info("SmallCityMilestoneSystem created");
        }

        protected override void OnUpdate() {

            if (!m_Done) {
                try {
                    m_SmallCityMilestones = SmallCity<SCMilestones>.Create(ref base.CheckedStateRef);
                    CustomMilestone hamlet = CustomMilestone.MakeHamlet();
                    var milestones = m_SmallCityMilestones.GetMilestoneInfo().GetMilestones(m_PrefabSystem);
                    MilestonePrefab hamletPrefab = m_SmallCityMilestones.Get().AddMilestone(hamlet, m_PrefabSystem);
                    Entity hamletEntity = m_SmallCityMilestones.GetMilestoneInfo().GetEntity(hamletPrefab, m_PrefabSystem);
                    EntityManager.AddComponent<CmpCustomMilestone>(hamletEntity);
                    m_SmallCityMilestones.Get().MoveMilestones(milestones, hamlet.Index, 750, 1);
                    var allMilestones = m_SmallCityMilestones.GetMilestoneInfo().GetMilestones(m_PrefabSystem);
                    var customMilestones = m_SmallCityMilestones.GetMilestoneInfo().GetCustomMilestones(m_PrefabSystem);
                    m_SmallCityMilestones.Get().UpdateMajorMilestones(allMilestones, customMilestones);
                    var tinyVillage = milestones.First(m => m.milestone.name == "Milestone 1");
                    m_SmallCityMilestones.Get().AddMapTilePoints(tinyVillage.entity, tinyVillage.milestone, -1);

                } finally {
                    m_Done = true;
                }

            }

            /*if (m_Logged) {
                if (!m_DeathcareMoved)
                    m_DeathcareMoved = MoveDeathcareToHamlet();

                return;
            }



            using (var prefabs = m_MilestoneQuery.ToComponentDataArray<PrefabData>(Allocator.Temp)) {
                if (prefabs.Length < 20)
                    return;

                var milestones = new List<MilestonePrefab>();

                foreach (var prefabData in prefabs) {
                    milestones.Add(m_PrefabSystem.GetPrefab<MilestonePrefab>(prefabData));
                }

                if (milestones.Exists(m => m.name == "Hamlet")) {
                    Mod.log.Info("Hamlet already registered");
                    m_Logged = true;
                    return;
                }

                var tinyVillage = milestones.Find(m => m.m_Index == 1);
                var smallVillage = milestones.Find(m => m.m_Index == 2);

                if (tinyVillage == null || smallVillage == null)
                    return;

                int xpOffset = smallVillage.m_XpRequried - tinyVillage.m_XpRequried;                

                // Create Hamlet from Tiny Village
                var hamlet = ScriptableObject.CreateInstance<MilestonePrefab>();
                hamlet.name = "Hamlet";
                hamlet.m_Index = 1;
                hamlet.m_XpRequried = tinyVillage.m_XpRequried;

                hamlet.m_Reward = 0;
                hamlet.m_DevTreePoints = 0;
                hamlet.m_MapTiles = 0;
                hamlet.m_LoanLimit = 0;
                hamlet.m_Major = false;
                hamlet.m_IsVictory = false;

                // Copy visual properties
                hamlet.m_Image = tinyVillage.m_Image;
                hamlet.m_BackgroundColor = tinyVillage.m_BackgroundColor;
                hamlet.m_AccentColor = tinyVillage.m_AccentColor;
                hamlet.m_TextColor = tinyVillage.m_TextColor;
                hamlet.AddComponent<ManualUnlockable>();

                if (!m_PrefabSystem.AddPrefab(hamlet)) {
                    Mod.log.Error("Failed to add Hamlet milestone");
                    UnityEngine.Object.Destroy(hamlet);
                    m_Logged = true;
                    return;
                }

                // Shift existing milestones
                foreach (var milestone in milestones) {
                    milestone.m_Index++;
                    milestone.m_XpRequried += xpOffset;
                    milestone.m_Major = milestone.m_Index == 6 ||
                                       milestone.m_Index == 10 ||
                                       milestone.m_Index == 14 ||
                                       milestone.m_Index == 18 ||
                                       milestone.m_Index == 21;

                    Entity entity = m_PrefabSystem.GetEntity(milestone);

                    // Keep already initialized ECS data synchronized
                    if (EntityManager.HasComponent<MilestoneData>(entity)) {
                        var data = EntityManager.GetComponentData<MilestoneData>(entity);

                        data.m_Index = milestone.m_Index;
                        data.m_XpRequried = milestone.m_XpRequried;
                        data.m_Major = milestone.m_Major;

                        EntityManager.SetComponentData(entity, data);
                    }

                    Mod.log.Info($"Milestone {milestone.name} Index {milestone.m_Index} XP {milestone.m_XpRequried}");
                }


                Mod.log.Info("Hamlet milestone registered");
                m_Logged = true;
            }*/


        }
        /*private bool MoveDeathcareToHamlet() {
            Entity hamletEntity = Entity.Null;

            using (var prefabs = m_MilestoneQuery.ToComponentDataArray<PrefabData>(Allocator.Temp)) {
                foreach (var prefabData in prefabs) {
                    var milestone = m_PrefabSystem.GetPrefab<MilestonePrefab>(prefabData);

                    if (milestone != null && milestone.name == "Hamlet") {
                        hamletEntity = m_PrefabSystem.GetEntity(milestone);
                        break;
                    }
                }
            }

            if (hamletEntity == Entity.Null)
                return false;
            int changes = 0;

            using (var prefabs = m_AllPrefabsQuery.ToComponentDataArray<PrefabData>(Allocator.Temp)) {
                foreach (var prefabData in prefabs) {
                    var prefab = m_PrefabSystem.GetPrefab<PrefabBase>(prefabData);

                    if (prefab == null ||
                        (prefab.name != "Deathcare" && prefab.name != "Cemetery02"))
                        continue;

                    Entity entity = m_PrefabSystem.GetEntity(prefab);

                    if (!EntityManager.TryGetBuffer<UnlockRequirement>(
                        entity, false, out var requirements))
                        continue;

                    for (int i = 0; i < requirements.Length; i++) {
                        var requirement = requirements[i];

                        if ((requirement.m_Flags & UnlockFlags.RequireAll) == 0)
                            continue;

                        var requiredPrefab = m_PrefabSystem.GetPrefab<PrefabBase>(
                            requirement.m_Prefab);

                        if (requiredPrefab == null)
                            continue;

                        bool move =
                            (prefab.name == "Deathcare" &&
                             requiredPrefab.name == "Health & Deathcare") ||
                            (prefab.name == "Cemetery02" &&
                             (requiredPrefab.name == "Health & Deathcare" ||
                              requiredPrefab.name == "BasicHealthcareNode"));

                        if (!move)
                            continue;

                        requirement.m_Prefab = hamletEntity;
                        requirements[i] = requirement;
                        changes++;

                        Mod.log.Info($"Changed {prefab.name} requirement from {requiredPrefab.name} to Hamlet");
                    }
                }
            }
            Mod.log.Info($"Deathcare requirements changed {changes}");
            return changes == 3;
        }*/
    }
}
