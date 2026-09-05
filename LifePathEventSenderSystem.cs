using Colossal.Localization;
using Colossal.Logging;
using Game;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Triggers;
using Game.UI;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.InputSystem;

namespace BitulaMod
{
    public partial class LifePathEventSenderSystem : GameSystemBase {

        public static ILog log = LogManager.GetLogger($"{nameof(BitulaMod)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Dictionary<CustomEventType, TriggerPrefab> m_EventPrefabs;
        private PrefabSystem m_PrefabSystem;
        private LifePathEventSystem m_LifePathEventSystem;
        private NativeQueue<CustomEvent> m_CustomEventQueue;
        private JobHandle m_ProducerDependency;
        private NameSystem m_NameSystem;
        private LocalizationManager m_LocaleManager;

        protected override void OnCreate() {
            base.OnCreate();
            m_LocaleManager = GameManager.instance.localizationManager;
            m_EventPrefabs = new Dictionary<CustomEventType, TriggerPrefab>();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_LifePathEventSystem = World.GetOrCreateSystemManaged<LifePathEventSystem>();
            m_CustomEventQueue = new NativeQueue<CustomEvent>(Allocator.Persistent);
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();

            foreach (CustomEventType type in Enum.GetValues(typeof(CustomEventType))) {
                TriggerPrefab prefab = new TriggerPrefab {
                    name = $"BitulaMod_{type}"
                };

                Game.Prefabs.LifePathEvent lifePathComponent =
                    prefab.AddOrGetComponent<Game.Prefabs.LifePathEvent>();

                lifePathComponent.m_EventType = (LifePathEventType)type;
                lifePathComponent.m_IsChirp = true;

                RandomLocalization randomLocalization =
                    prefab.AddOrGetComponent<RandomLocalization>();

                randomLocalization.m_LocalizationID =
                    $"BitulaMod.LIFEPATH_{type}";

                m_PrefabSystem.AddPrefab(prefab);

                m_EventPrefabs[type] = prefab;
            }
        }

        

        protected override void OnDestroy()
        {
            if (m_CustomEventQueue.IsCreated)
                m_CustomEventQueue.Dispose();

            base.OnDestroy();
        }

        public NativeQueue<CustomEvent>.ParallelWriter GetQueueWriter()
        {
            return m_CustomEventQueue.AsParallelWriter();
        }

        public void AddProducer(JobHandle jobHandle)
        {
            m_ProducerDependency = JobHandle.CombineDependencies(m_ProducerDependency, jobHandle);
        }

        protected override void OnUpdate() {
            if (!m_ProducerDependency.IsCompleted)
            {
                log.Info("LifePath sender: producer still running");
                return;
            }

            m_ProducerDependency.Complete();

            int count = 0;

            while (m_CustomEventQueue.TryDequeue(out CustomEvent cevent))
            {
                count++;
                SendCitizenEvent(cevent);
            }

            if (count > 0)
                log.Info($"LifePath sender drained {count} event(s)");

            m_ProducerDependency = default;
        }

        public void SendCitizenEvent(CustomEvent cevent) {
            TriggerPrefab prefab = m_EventPrefabs[cevent.m_EventType];
            Entity eventPrefab = m_PrefabSystem.GetEntity(prefab);

            NativeQueue<LifePathEventCreationData> queue =
                m_LifePathEventSystem.GetQueue(out JobHandle deps);

            deps.Complete();

            Entity parameterEntity = EntityManager.CreateEntity();

            string[] parameters = cevent.m_Param.ToString().Split(',');

            string key = $"BitulaMod.LIFEPATH_LINK_{cevent.m_EventType}";

            if (!m_LocaleManager.activeDictionary.TryGetValue(key, out string template)) {
                template = key;
            }

            string parameterText;

            if (parameters.Length > 0)
                parameterText = string.Format(template, parameters);
            else
                parameterText = template;

            m_NameSystem.SetCustomName(
                parameterEntity,
                parameterText
            );

            queue.Enqueue(new LifePathEventCreationData {
                m_EventPrefab = eventPrefab,
                m_Sender = cevent.m_Citizen,
                m_Target = parameterEntity
            });

            string parameterName = m_NameSystem.GetRenderedLabelName(parameterEntity);

            Mod.log.Info(
                $"Parameter entity: {parameterEntity}, name: {parameterName}");

            string citizenName =
                m_NameSystem.GetRenderedLabelName(cevent.m_Citizen);

            Mod.log.Info(
                $"{cevent.m_EventType} chirp enqueued for {citizenName} ({cevent.m_Citizen})");
        }
    }
}
