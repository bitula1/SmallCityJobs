using Colossal.Localization;
using Colossal.Logging;
using Game;
using Game.Buildings;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Triggers;
using Game.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Burst.CompilerServices;
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
        private CreateChirpSystem m_CreateChirpSystem;
        private LifePathEventSystem m_LifePathEventSystem;
        private NativeQueue<CustomEvent> m_CustomEventQueue;
        private JobHandle m_ProducerDependency;
        private NameSystem m_NameSystem;
        private LocalizationManager m_LocaleManager;
        private readonly Dictionary<Entity, MessageInfo> m_Events = new();
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        

        protected override void OnCreate() {
            base.OnCreate();
            m_LocaleManager = GameManager.instance.localizationManager;
            m_EventPrefabs = new Dictionary<CustomEventType, TriggerPrefab>();

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            m_LifePathEventSystem = World.GetOrCreateSystemManaged<LifePathEventSystem>();
            m_CreateChirpSystem = World.GetOrCreateSystemManaged<CreateChirpSystem>();
            m_CustomEventQueue = new NativeQueue<CustomEvent>(Allocator.Persistent);
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_PropertyRenters = SystemAPI.GetComponentLookup<PropertyRenter>(true);

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
            if (m_CustomEventQueue.IsEmpty())
                return;

            if (!m_ProducerDependency.IsCompleted)
            {
                log.Info("LifePath sender: producer still running");
                return;
            }
            m_PropertyRenters.Update(this);

            m_ProducerDependency.Complete();

            int count = 0;

            while (m_CustomEventQueue.TryDequeue(out CustomEvent cevent))
            {
                count++;
                SendCitizenEvent(cevent);
            }

            m_ProducerDependency = default;
        }

        public void SendCitizenEvent(CustomEvent cevent) {
            TriggerPrefab prefab = m_EventPrefabs[cevent.m_EventType];
            Entity eventPrefab = m_PrefabSystem.GetEntity(prefab);

            NativeQueue<LifePathEventCreationData> queue =
                m_LifePathEventSystem.GetQueue(out JobHandle deps);

            deps.Complete();

            MessageInfo message;

            if (!m_Events.TryGetValue(cevent.m_Citizen, out message)) {
                message = new MessageInfo(cevent);
                m_Events.Add(cevent.m_Citizen, message);
            } else {
                message.SetEvent(cevent);
            }

            if (message.HasEventsToWatch()) {
                if (message.HasHint(EventHint.CounterWatchEvent)) {
                    if (message.HasHint(EventHint.DirectPreviousMessage)) {
                        if (message.LastEventMatchesWatch(EventHint.CounterWatchEvent))
                            return;
                    } else if (message.HasMatchingWatchedEvent(EventHint.CounterWatchEvent)) {
                        return;
                    }
                }

                if (message.HasHint(EventHint.WatchEvent) &&
                    !message.HasMatchingWatchedEvent(EventHint.WatchEvent)) {
                    return;
                }
            }

            

            if (cevent.m_EventType != CustomEventType.DebugMessage) {
                string eventKey = message.GetEventText();
                string lastEventKey = message.GetLastEventText();

                if (lastEventKey == eventKey)
                    return;                
            }

            Entity parameterEntity;

            if (cevent.m_Target != Entity.Null) {
                NativeQueue<ChirpCreationData> chirpQueue =
                    m_CreateChirpSystem.GetQueue(out JobHandle chirpDeps);

                chirpDeps.Complete();

                chirpQueue.Enqueue(new ChirpCreationData {
                    m_TriggerPrefab = eventPrefab,
                    m_Sender = cevent.m_Citizen,
                    m_Target = cevent.m_Target
                });

                return;
            }

            parameterEntity = EntityManager.CreateEntity();

            m_NameSystem.SetCustomName(
                parameterEntity,
                message.GetParameterText()
            );


            queue.Enqueue(new LifePathEventCreationData {
                m_EventPrefab = eventPrefab,
                m_Sender = cevent.m_Citizen,
                m_Target = parameterEntity
            });

            if (message.HasEventsToWatch())
                message.RemoveEventsWatched();
            if (cevent.m_EventType != CustomEventType.DebugMessage)
                message.StoreCurrentEvent();
        }
    }
}
