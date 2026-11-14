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
        private const int FinalMilestoneXp = 680000;
        private const int FirstMajorMilestoneIndex = 0;
        private const int LastMajorMilestoneIndex = 20;
        private const int MajorMilestoneInterval = 4;
        private SCCommon m_Common;
        private bool m_VillageProgression;
        private SCInfo<SCMilestoneInfo> m_MilestoneInfo;
        private SCInfo<SCPrefabInfo> m_PrefabInfo;
        private ComponentLookup<MilestoneData> m_Milestones;
        private BufferLookup<UnlockRequirement> m_UnlockRequirements;
        private EntityQuery m_MenuQuery;
        private EntityQuery m_InfoviewQuery;
        private ComponentLookup<ForceUnlockRequirementData> m_ForceUnlockRequirements;
        private BufferLookup<ForceUIGroupUnlockData> m_ForcedUIUnlocks;
        private ComponentLookup<Locked> m_Locked;
        private EntityQuery m_HealthcareSettingsQuery;

        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }

        public void LogRoadMenus() {
            var prefabSystem = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<PrefabSystem>();

            var prefabInfo = m_PrefabInfo.Get();

            string[] menuNames = {
        "RoadsMediumRoads",
        "RoadsSmallRoads"
    };

            foreach (string menuName in menuNames) {
                Entity entity;

                try {
                    entity = prefabInfo.GetEntity<UIAssetCategoryPrefab>(
                        menuName, prefabSystem);
                } catch (ArgumentException) {
                    Mod.log.Warn($"Road menu not found: {menuName}");
                    continue;
                }

                var prefab = prefabInfo.GetPrefab<PrefabBase>(
                    entity, prefabSystem);

                Mod.log.Info(
                    $"Road menu={menuName} Entity={entity} Type={prefab?.GetType().Name}");

                if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements)) {
                    Mod.log.Warn($"Road menu={menuName} has no UnlockRequirement buffer");
                    continue;
                }

                foreach (var requirement in requirements) {
                    var requiredPrefab = prefabInfo.GetPrefab<PrefabBase>(
                        requirement.m_Prefab, prefabSystem);

                    Mod.log.Info(
                        $"Road menu={menuName} " +
                        $"Requires={requiredPrefab?.name ?? "Unknown"} " +
                        $"Type={requiredPrefab?.GetType().Name ?? "Unknown"} " +
                        $"Flags={requirement.m_Flags}");
                }
            }
        }

        public SCMilestones Create(ref SystemState state, in SCCommon common) {
            return new SCMilestones {
                m_Common = common,
                m_VillageProgression = Mod.Settings.VillageProgression,
                m_Milestones = state.GetComponentLookup<MilestoneData>(false),
                m_MilestoneInfo = SCInfo<SCMilestoneInfo>.Create(ref state),
                m_PrefabInfo = SCInfo<SCPrefabInfo>.Create(ref state),
                m_UnlockRequirements = state.GetBufferLookup<UnlockRequirement>(false),
                m_MenuQuery = state.GetEntityQuery(ComponentType.ReadOnly<UIAssetMenuData>(), ComponentType.ReadOnly<PrefabData>()),
                m_InfoviewQuery = state.GetEntityQuery( ComponentType.ReadOnly<InfoviewData>(), ComponentType.ReadOnly<PrefabData>()),
                m_ForceUnlockRequirements = state.GetComponentLookup<ForceUnlockRequirementData>(false),
                m_ForcedUIUnlocks = state.GetBufferLookup<ForceUIGroupUnlockData>(true),
                m_Locked = state.GetComponentLookup<Locked>(true),
                m_HealthcareSettingsQuery = state.GetEntityQuery(ComponentType.ReadOnly<HealthcareParameterData>()),
            };
        }

        public SCMilestones Create() {
            return new SCMilestones {
                m_VillageProgression = Mod.Settings.VillageProgression
            };
        }

        public bool IsDeathCareEnabled() {
            if (m_VillageProgression)
                return IsPrefabUnlocked<FeaturePrefab>("Deathcare");

            if (m_HealthcareSettingsQuery.IsEmptyIgnoreFilter)
                return false;

            Entity entity = m_HealthcareSettingsQuery
                .GetSingleton<HealthcareParameterData>()
                .m_HealthcareServicePrefab;

            return !m_Locked.HasComponent(entity) || !m_Locked.IsComponentEnabled(entity);
        }

        public bool IsElectricityEnabled() {
            return !m_VillageProgression ||
                IsPrefabUnlocked<UIAssetCategoryPrefab>("Electricity");
        }

        public bool IsWaterEnabled() {
            return !m_VillageProgression ||
                IsPrefabUnlocked<UIAssetCategoryPrefab>("Water & Sewage");
        }

        private bool AddLockRequirement(Entity entity, Entity requirementEntity) {
            if (entity == Entity.Null || requirementEntity == Entity.Null) {
                Mod.log.Warn($"Cannot lock entity {entity} with requirement {requirementEntity}: Invalid entity");
                return false;
            }

            var requirement = new UnlockRequirement {
                m_Prefab = requirementEntity,
                m_Flags = UnlockFlags.RequireAll
            };

            if (m_UnlockRequirements.TryGetBuffer(entity, out var requirements)) {
                foreach (var existing in requirements) {
                    if (existing.m_Prefab == requirementEntity &&
                        (existing.m_Flags & UnlockFlags.RequireAll) != 0)
                        return true;
                }

                requirements.Add(requirement);
                return true;
            }

            if (!m_Common.m_ECB.IsCreated) {
                Mod.log.Warn($"Cannot lock entity {entity}: UnlockRequirement buffer not found and ECB unavailable");
                return false;
            }

            var newRequirements = m_Common.m_ECB.AddBuffer<UnlockRequirement>(entity);
            newRequirements.Add(requirement);

            if (!m_Locked.HasComponent(entity))
                m_Common.m_ECB.AddComponent<Locked>(entity);

            Mod.log.Info($"Created UnlockRequirement buffer and Locked component for entity {entity}");
            return true;
        }

        public bool LockInfoviewTo(string requirementName, string infoviewName) {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

            Entity requirementEntity =
                m_PrefabInfo.Get().GetEntity<MilestonePrefab>(requirementName, prefabSystem);

            if (requirementEntity == Entity.Null) {
                Mod.log.Warn($"Cannot find requirement {requirementName}");
                return false;
            }

            using (var entities = m_InfoviewQuery.ToEntityArray(Allocator.Temp)) {
                foreach (var entity in entities) {
                    var prefab = m_PrefabInfo.Get().GetPrefab<InfoviewPrefab>(entity, prefabSystem);

                    if (prefab?.name != infoviewName)
                        continue;

                    if (!AddLockRequirement(entity, requirementEntity)) {
                        Mod.log.Warn($"Cannot lock infoview {infoviewName}");
                        return false;
                    }

                    Mod.log.Info($"Infoview {infoviewName} locked to {requirementName}");
                    return true;
                }
            }

            Mod.log.Warn($"Cannot find infoview {infoviewName}");
            return false;
        }

        public bool RemoveMenuUnlock<T>(string prefabName, string menuName) where T : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();

            Entity entity = m_PrefabInfo.Get().GetEntity<T>(prefabName, prefabSystem);

            if (entity == Entity.Null) {
                Mod.log.Warn($"Prefab {prefabName} not found");
                return false;
            }

            if (!m_ForcedUIUnlocks.TryGetBuffer(entity, out var forcedUnlocks)) {
                Mod.log.Warn($"Forced unlock buffer not found for {prefabName}");
                return false;
            }

            bool found = false;

            foreach (var entry in forcedUnlocks) {
                var target = m_PrefabInfo.Get().GetPrefab<PrefabBase>(
                    entry.m_Entity, prefabSystem);

                if (target is UIAssetMenuPrefab && target.name == menuName) {
                    found = true;
                    break;
                }
            }

            if (!found) {
                Mod.log.Warn($"Menu {menuName} not found in forced unlocks of {prefabName}");
                return false;
            }

            var newBuffer = m_Common.SetBuffer<ForceUIGroupUnlockData>(entity);

            foreach (var entry in forcedUnlocks) {
                var target = m_PrefabInfo.Get().GetPrefab<PrefabBase>(
                    entry.m_Entity, prefabSystem);

                if (target is UIAssetMenuPrefab && target.name == menuName)
                    continue;

                newBuffer.Add(entry);
            }

            Mod.log.Info($"Removed forced menu unlock from {prefabName} Target={menuName}");
            return true;
        }

        public bool LockMenuTo<T>(string requirementName, string menuName) where T : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            Entity requirementEntity = m_PrefabInfo.Get().GetEntity<T>(requirementName, prefabSystem);
            if (requirementEntity == Entity.Null) {
                Mod.log.Warn($"Cannot find requirement {requirementName}");
                return false;
            }

            using (var entities = m_MenuQuery.ToEntityArray(Allocator.Temp)) {
                foreach (var entity in entities) {
                    var prefab = m_PrefabInfo.Get().GetPrefab<PrefabBase>(entity, prefabSystem);

                    if (prefab?.name != menuName)
                        continue;

                    if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements)) {
                        Mod.log.Warn($"Cannot find requirements for menu {menuName}");
                        return false;
                    }

                    requirements.Clear();

                    requirements.Add(new UnlockRequirement {
                        m_Prefab = requirementEntity,
                        m_Flags = UnlockFlags.RequireAll
                    });

                    var forced = new ForceUnlockRequirementData {
                        m_Prefab = entity
                    };

                    if (m_ForceUnlockRequirements.HasComponent(entity))
                        m_Common.SetComponent(entity, forced);
                    else
                        m_Common.AddComponent(entity, forced);

                    Mod.log.Info($"Menu {menuName} requirement replaced with {requirementName}");
                    return true;
                }
            }

            Mod.log.Warn($"Cannot find menu {menuName}");
            return false;
        }
       
        public bool LockRequirementTo<TRequirement, TTarget>(string requirementName, string targetPrefabName) 
            where TRequirement : PrefabBase where TTarget : PrefabBase {

            var prefabSystem = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<PrefabSystem>();

            Entity requirementEntity = m_PrefabInfo.Get()
                .GetEntity<TRequirement>(requirementName, prefabSystem);

            Entity targetEntity = m_PrefabInfo.Get()
                .GetEntity<TTarget>(targetPrefabName, prefabSystem);

            return AddLockRequirement(targetEntity, requirementEntity);
        }

        public bool UnlockRequirementFrom<TRequirement, TTarget>( string targetPrefabName, string requirementName)
            where TRequirement : PrefabBase  where TTarget : PrefabBase {

            return UnlockRequirementTo<TRequirement, TTarget>(requirementName, targetPrefabName);
        }

        public bool UnlockRequirementTo<TRequirement, TTarget>(string requirementName, string targetPrefabName)
            where TRequirement : PrefabBase  where TTarget : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            Entity requirementEntity = m_PrefabInfo.Get().GetEntity<TRequirement>(requirementName, prefabSystem);
            Entity targetEntity = m_PrefabInfo.Get().GetEntity<TTarget>(targetPrefabName, prefabSystem);

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

        public bool AddUnlockRequirementToAny<TRequirement, TExisting>( string requirementName, string existingRequirementName)
            where TRequirement : PrefabBase where TExisting : PrefabBase {

            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            var prefabInfo = m_PrefabInfo.Get();

            Entity requirementEntity = prefabInfo.GetEntity<TRequirement>(requirementName, prefabSystem);
            Entity existingEntity = prefabInfo.GetEntity<TExisting>(existingRequirementName, prefabSystem);

            if (!m_UnlockRequirements.TryGetBuffer(existingEntity, out var requirements)) {
                Mod.log.Warn($"No unlock requirements found for GUI prefab {existingRequirementName}");
                return false;
            }

            bool found = false;
            bool success = true;

            foreach (var requirement in requirements) {
                if ((requirement.m_Flags & UnlockFlags.RequireAny) == 0)
                    continue;

                Entity entity = requirement.m_Prefab;

                if (entity == Entity.Null)
                    continue;

                found = true;
                success &= AddLockRequirement(entity, requirementEntity);
            }

            if (!found)
                Mod.log.Warn($"No prefabs found in GUI category {existingRequirementName}");

            return found && success;
        }

        public bool AddUnlockRequirementToAll<TRequirement, TExisting>( string requirementName, string existingRequirementName)
            where TRequirement : PrefabBase where TExisting : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            var prefabInfo = m_PrefabInfo.Get();

            Entity requirementEntity = prefabInfo.GetEntity<TRequirement>(requirementName, prefabSystem);
            Entity existingEntity = prefabInfo.GetEntity<TExisting>(existingRequirementName, prefabSystem);

            bool found = false;
            bool success = true;

            foreach (var prefab in prefabInfo.GetAllPrefabs<PrefabBase>(prefabSystem)) {
                if (prefab == null)
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

                if (!hasRequirement)
                    continue;

                found = true;
                success &= AddLockRequirement(entity, requirementEntity);
            }

            if (!found)
                Mod.log.Warn($"No prefabs found requiring {existingRequirementName}");

            return found && success;
        }

        private double CalculateXpMultiplierDecrease(double startXp, double targetXp, int milestoneCount) {

            double low = 0.0;
            double high = InitialXpMultiplier / milestoneCount;

            for (int iteration = 0; iteration < 100; iteration++) {
                double decrease = (low + high) / 2.0;
                double xp = startXp;
                double multiplier = InitialXpMultiplier;

                for (int i = 0; i < milestoneCount; i++) {
                    xp *= multiplier;
                    multiplier -= decrease;
                }

                if (xp > targetXp)
                    low = decrease;
                else
                    high = decrease;
            }

            return (low + high) / 2.0;
        }

        public void MoveToIndex(Entity entity, MilestonePrefab milestone, int index, int xpRequired) {
            milestone.m_Index = index;
            milestone.m_XpRequried = xpRequired;

            if (m_MilestoneInfo.Get().TryGetMilestone(entity, out MilestoneData data)) {
                data.m_Index = index;
                data.m_XpRequried = xpRequired;

                SetMilestone(entity, data);
            }
        }

        public void MoveMilestones( List<(Entity entity, MilestonePrefab milestone)> milestones, 
            int fromIndex, int insertedXp = 750, double intervalScale = 1.0) {

            var affected = milestones
                .Where(m => m.milestone.m_Index >= fromIndex)
                .OrderBy(m => m.milestone.m_Index)
                .ToList();

            double xp = insertedXp;
            double multiplier = InitialXpMultiplier;

            double multiplierDecrease = CalculateXpMultiplierDecrease(
                insertedXp, FinalMilestoneXp, affected.Count);

            foreach (var (entity, milestone) in affected) {
                xp *= multiplier;

                int newXp = (int)System.Math.Round(
                    insertedXp + (xp - insertedXp) * intervalScale);

                MoveToIndex(entity, milestone, milestone.m_Index + 1, newXp);

                multiplier -= multiplierDecrease;
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

        public bool MoveRequirementTo<TTarget, TPrerequisite>(string prefabName, string milestoneName, string prerequisiteName)
            where TTarget : PrefabBase where TPrerequisite : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            Entity milestoneEntity = Entity.Null;

            var milestones = m_MilestoneInfo.Get().GetMilestones(prefabSystem);

            foreach (var (entity, milestone) in milestones) {
                if (milestone.name == milestoneName) {
                    milestoneEntity = entity;
                    break;
                }
            }

            if (milestoneEntity == Entity.Null) {
                Mod.log.Warn($"Cannot move requirement: milestone {milestoneName} not found");
                return false;
            }

            int changes = 0;
            Entity prerequisiteEntity = m_PrefabInfo.Get().GetEntity<TPrerequisite>(prerequisiteName, prefabSystem);

            foreach (var prefab in m_PrefabInfo.Get().GetAllPrefabs<TTarget>(prefabSystem)) {
                if (prefab == null || prefab.name != prefabName)
                    continue;

                Entity entity = m_PrefabInfo.Get().GetEntity(prefab, prefabSystem);

                if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements))
                    continue;

                for (int i = 0; i < requirements.Length; i++) {
                    var requirement = requirements[i];

                    if ((requirement.m_Flags & UnlockFlags.RequireAll) == 0)
                        continue;

                    if (requirement.m_Prefab != prerequisiteEntity)
                        continue;

                    requirement.m_Prefab = milestoneEntity;
                    requirements[i] = requirement;
                    changes++;
                }
            }

            if (changes == 0)
                Mod.log.Warn($"Cannot move requirement for {prefabName} from {prerequisiteName} to {milestoneName}");

            return changes > 0;
        }

        public bool MoveUnlockRequirementTo<TTarget, TUnlockRequirement, TMoveRequirement>(string prefabName, string milestoneName, string unlockRequirementName, string moveRequirementName)
            where TTarget : PrefabBase where TUnlockRequirement : PrefabBase where TMoveRequirement : PrefabBase {

            bool done = true;
            done &= UnlockRequirementTo<TUnlockRequirement, TTarget>(unlockRequirementName, prefabName);
            done &= MoveRequirementTo<TTarget, TMoveRequirement>(prefabName, milestoneName, moveRequirementName);
            return done;
        }

        public void AddMapTilePoints( List<(Entity entity, MilestonePrefab milestone)> milestones, string milestoneName, int points) {

            var (entity, milestone) = milestones.First(m => m.milestone.name == milestoneName);

            milestone.m_MapTiles = Math.Max(0, milestone.m_MapTiles + points);

            if (m_MilestoneInfo.Get().TryGetMilestone(entity, out MilestoneData data)) {
                data.m_MapTiles = milestone.m_MapTiles;
                SetMilestone(entity, data);
            }

            Mod.log.Info($"Milestone {milestone.name} Map tiles {milestone.m_MapTiles}");
        }

        public bool RemoveUnlockRequirements<T>(string prefabName) where T : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<PrefabSystem>();
            Entity entity = m_PrefabInfo.Get().GetEntity<T>(prefabName, prefabSystem);


            if (entity == Entity.Null) {
                Mod.log.Warn($"Cannot find prefab {prefabName}");
                return false;
            }

            if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements)) {
                Mod.log.Warn($"Cannot find unlock requirements for {prefabName}");
                return false;
            }

            requirements.Clear();

            Mod.log.Info($"Removed unlock requirements from {prefabName}");
            return true;
        }

        public bool IsPrefabUnlocked<T>(string prefabName) where T : PrefabBase {
            var prefabSystem = World.DefaultGameObjectInjectionWorld
                .GetOrCreateSystemManaged<PrefabSystem>();

            Entity entity = m_PrefabInfo.Get().GetEntity<T>(prefabName, prefabSystem);

            return !m_Locked.HasComponent(entity) || !m_Locked.IsComponentEnabled(entity);
        }

        public void Update(ref SystemState state) {
            m_Common.Update(ref state);
            m_Milestones.Update(ref state);
            m_MilestoneInfo.Update(ref state);
            m_PrefabInfo.Update(ref state);
            m_UnlockRequirements.Update(ref state);
            m_ForceUnlockRequirements.Update(ref state);
            m_ForcedUIUnlocks.Update(ref state);
            m_Locked.Update(ref state);
        }

        public SCMilestoneInfo GetInfo() {
            return m_MilestoneInfo.Get();
        }

        public SCPrefabInfo GetPrefabInfo() {
            return m_PrefabInfo.Get();
        }

        public void LogUnlockRequirements<T>(string prefabName, PrefabSystem prefabSystem) where T : PrefabBase {
            var prefabInfo = m_PrefabInfo.Get();

            Entity entity = prefabInfo.GetEntity<T>(prefabName, prefabSystem);
            var prefab = prefabInfo.GetPrefab<PrefabBase>(entity, prefabSystem);

            Mod.log.Info(
                $"Prefab={prefabName} Type={prefab?.GetType().Name} Entity={entity}");

            if (!m_UnlockRequirements.TryGetBuffer(entity, out var requirements)) {
                Mod.log.Info($"{prefabName}: No UnlockRequirement buffer");
                return;
            }

            Mod.log.Info($"{prefabName}: {requirements.Length} unlock requirements");

            for (int i = 0; i < requirements.Length; i++) {
                var requirement = requirements[i];

                var requiredPrefab = prefabInfo.GetPrefab<PrefabBase>(
                    requirement.m_Prefab, prefabSystem);

                Mod.log.Info(
                    $"{prefabName}: Requirement[{i}] = {requiredPrefab?.name ?? "UNKNOWN"} Flags={requirement.m_Flags}");
            }
        }}
    }