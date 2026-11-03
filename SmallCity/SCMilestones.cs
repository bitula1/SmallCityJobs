using Unity.Entities;

using Game.Prefabs;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using System;

namespace BitulaMod.SmallCity {
    public struct SCMilestones : ISCFeature<SCMilestones> {
        private const double InitialXpMultiplier = 1.8;
        private const double XpMultiplierDecrease = 0.0396;
        private const int FirstMajorMilestoneIndex = 5;
        private const int LastMajorMilestoneIndex = 20;
        private const int MajorMilestoneInterval = 4;
        private SCCommon m_Common;
        private SCInfo<SCMilestoneInfo> m_MilestoneInfo;
        private SCInfo<SCPrefabInfo> m_PrefabInfo;
        private ComponentLookup<MilestoneData> m_Milestones;
        private BufferLookup<UnlockRequirement> m_UnlockRequirements;

        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }
        
        private void AddLockRequirement(Entity entity, Entity requirementEntity) {
            if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements))
                return;

            foreach (var requirement in requirements) {
                if (requirement.m_Prefab == requirementEntity &&
                    (requirement.m_Flags & UnlockFlags.RequireAll) != 0)
                    return;
            }

            requirements.Add(new UnlockRequirement {
                m_Prefab = requirementEntity,
                m_Flags = UnlockFlags.RequireAll
            });
        }

        public void LockRequirementTo(string requirementName,  string targetPrefabName, PrefabSystem prefabSystem) {

            Entity requirementEntity = m_PrefabInfo.Get().GetEntity(requirementName, prefabSystem);
            Entity targetEntity = m_PrefabInfo.Get().GetEntity(targetPrefabName, prefabSystem);

            AddLockRequirement(targetEntity, requirementEntity);
        }

        public bool UnlockRequirementTo( string requirementName, string targetPrefabName, PrefabSystem prefabSystem) {

            Entity requirementEntity = m_PrefabInfo.Get().GetEntity(requirementName, prefabSystem);
            Entity targetEntity = m_PrefabInfo.Get().GetEntity(targetPrefabName, prefabSystem);

            if (!m_UnlockRequirements.TryGetBuffer(targetEntity, out var requirements))
                return false;

            for (int i = 0; i < requirements.Length; i++) {
                if (requirements[i].m_Prefab == requirementEntity &&
                    (requirements[i].m_Flags & UnlockFlags.RequireAll) != 0) {

                    requirements.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public void LockRequirementToAll( string requirementName,  string existingRequirementName, PrefabSystem prefabSystem) {

            var prefabInfo = m_PrefabInfo.Get();
            var allPrefabs = prefabInfo.GetAllPrefabs<PrefabBase>(prefabSystem);

            Entity requirementEntity = m_PrefabInfo.Get().GetEntity(requirementName, prefabSystem);
            Entity existingEntity = m_PrefabInfo.Get().GetEntity(existingRequirementName, prefabSystem);

            foreach (var prefab in allPrefabs) {
                if (!(prefab is BuildingPrefab))
                    continue;

                Entity entity = prefabInfo.GetEntity(prefab, prefabSystem);

                if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements))
                    continue;

                bool hasRequirement = false;

                foreach (var requirement in requirements) {
                    if (requirement.m_Prefab == existingEntity &&
                        (requirement.m_Flags & UnlockFlags.RequireAll) != 0) {
                        hasRequirement = true;
                        break;
                    }
                }

                if (hasRequirement)
                    AddLockRequirement(entity, requirementEntity);
            }
        }
        
        public SCMilestones Create(ref SystemState state, in SCCommon common) {
            return new SCMilestones {
                m_Common = common,
                m_Milestones = state.GetComponentLookup<MilestoneData>(false),
                m_MilestoneInfo = SCInfo<SCMilestoneInfo>.Create(ref state),
                m_PrefabInfo = SCInfo<SCPrefabInfo>.Create(ref state),
                m_UnlockRequirements = state.GetBufferLookup<UnlockRequirement>(false)
            };
        }

        public SCMilestones Create() {
            return new SCMilestones {
            };
        }

        public void MoveToIndex(Entity entity, MilestonePrefab milestone, int index, int xpOffset) {
            milestone.m_Index = index;
            milestone.m_XpRequried += xpOffset;

            if (m_MilestoneInfo.Get().TryGetMilestone(entity, out MilestoneData data)) {
                data.m_Index = milestone.m_Index;
                data.m_XpRequried = milestone.m_XpRequried;

                SetMilestone(entity, data);
            }

            Mod.log.Info($"Milestone {milestone.name} Index {milestone.m_Index} XP {milestone.m_XpRequried}");
        }

        public void MoveMilestones( List<(Entity entity, MilestonePrefab milestone)> milestones, int fromIndex,
            int insertedXp = 750, double intervalScale = 1.0) {

            var affected = milestones
                .Where(m => m.milestone.m_Index >= fromIndex)
                .OrderBy(m => m.milestone.m_Index)
                .ToList();

            double xp = insertedXp;
            double multiplier = InitialXpMultiplier;

            foreach (var (entity, milestone) in affected) {
                xp *= multiplier;

                int newXp = (int)System.Math.Round(
                    insertedXp + (xp - insertedXp) * intervalScale);

                int xpOffset = newXp - milestone.m_XpRequried;

                MoveToIndex(entity, milestone, milestone.m_Index + 1, xpOffset);

                multiplier -= XpMultiplierDecrease;
            }
        }

        public void SetMajor(Entity entity, MilestonePrefab milestone, bool major) {
            milestone.m_Major = major;

            if (m_MilestoneInfo.Get().TryGetMilestone(entity, out MilestoneData data)) {
                data.m_Major = major;
                SetMilestone(entity, data);
            }
        }

        public void UpdateMajorMilestones( List<(Entity entity, MilestonePrefab milestone)> milestones,
            List<(Entity entity, MilestonePrefab milestone)> customMilestones) {

            var customEntities = new HashSet<Entity>(customMilestones.Select(m => m.entity));

            milestones.Sort((a, b) => a.milestone.m_Index.CompareTo(b.milestone.m_Index));

            int vanillaCount = milestones.Count(m => !customEntities.Contains(m.entity));
            int vanillaIndex = 0;

            for (int i = 0; i < milestones.Count; i++) {
                var (entity, milestone) = milestones[i];

                bool major;

                if (customEntities.Contains(entity)) {
                    major = milestone.m_Major;
                } else {
                    vanillaIndex++;

                    major = vanillaIndex == vanillaCount ||
                            (vanillaIndex >= FirstMajorMilestoneIndex &&
                             (vanillaIndex - FirstMajorMilestoneIndex) % MajorMilestoneInterval == 0);
                }

                if (i == milestones.Count - 1)
                    major = true;

                SetMajor(entity, milestone, major);
            }
        }

        public void SetMilestone(Entity entity, MilestoneData data) {
            m_Milestones[entity] = data;
        }

        public MilestonePrefab AddMilestone(CustomMilestone definition, PrefabSystem prefabSystem) {
            var milestone = ScriptableObject.CreateInstance<MilestonePrefab>();

            milestone.name = definition.Name;
            milestone.m_Index = definition.Index;
            milestone.m_XpRequried = definition.XpRequired;
            milestone.m_Reward = definition.Reward;
            milestone.m_DevTreePoints = definition.DevTreePoints;
            milestone.m_MapTiles = definition.MapTiles;
            milestone.m_LoanLimit = definition.LoanLimit;
            milestone.m_Major = definition.Major;
            milestone.m_IsVictory = definition.IsVictory;

            milestone.m_Image = definition.Image;
            milestone.m_BackgroundColor = definition.BackgroundColor;
            milestone.m_AccentColor = definition.AccentColor;
            milestone.m_TextColor = definition.TextColor;

            milestone.AddComponent<ManualUnlockable>();

            if (!prefabSystem.AddPrefab(milestone)) {
                Mod.log.Error($"Failed to add milestone {definition.Name}");
                UnityEngine.Object.Destroy(milestone);
                return null;
            }

            return milestone;
        }

        public bool MoveRequirementTo(string prefabName, string milestoneName,
            string prerequisiteName, PrefabSystem prefabSystem) {

            Entity milestoneEntity = Entity.Null;

            var milestones = m_MilestoneInfo.Get().GetMilestones(prefabSystem);

            foreach (var (entity, milestone) in milestones) {
                if (milestone.name == milestoneName) {
                    milestoneEntity = entity;
                    break;
                }
            }

            if (milestoneEntity == Entity.Null)
                return false;

            int changes = 0;

            foreach (var prefab in m_PrefabInfo.Get().GetAllPrefabs<PrefabBase>(prefabSystem)) {
                if (prefab == null || prefab.name != prefabName)
                    continue;

                Entity entity = m_PrefabInfo.Get().GetEntity(prefab, prefabSystem);

                if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements))
                    continue;

                for (int i = 0; i < requirements.Length; i++) {
                    var requirement = requirements[i];

                    if ((requirement.m_Flags & UnlockFlags.RequireAll) == 0)
                        continue;

                    var requiredPrefab = prefabSystem.GetPrefab<PrefabBase>(requirement.m_Prefab);

                    if (requiredPrefab == null || requiredPrefab.name != prerequisiteName)
                        continue;

                    requirement.m_Prefab = milestoneEntity;
                    requirements[i] = requirement;
                    changes++;

                    Mod.log.Info($"Moved {prefabName} requirement from {prerequisiteName} to {milestoneName}");
                }
            }

            return changes > 0;
        }

        public void AddMapTilePoints(Entity entity, MilestonePrefab milestone, int points) {
            milestone.m_MapTiles = Math.Max(0, milestone.m_MapTiles + points);

            if (m_MilestoneInfo.Get().TryGetMilestone(entity, out MilestoneData data)) {
                data.m_MapTiles = milestone.m_MapTiles;
                SetMilestone(entity, data);
            }

            Mod.log.Info($"Milestone {milestone.name} Map tiles {milestone.m_MapTiles}");
        }

        public void Update(ref SystemState state) {
            m_Common.Update(ref state);
            m_Milestones.Update(ref state);
            m_MilestoneInfo.Update(ref state);
            m_PrefabInfo.Update(ref state);
            m_UnlockRequirements.Update(ref state);
        }

        public SCMilestoneInfo GetInfo() {
            return m_MilestoneInfo.Get();
        }
    }
}