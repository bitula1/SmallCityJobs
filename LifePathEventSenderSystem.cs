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
using System.Linq;
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
        private readonly Dictionary<Entity, EventInfo> m_LastEvent = new();
        private readonly Dictionary<Entity, Dictionary<CustomEventType, MessageInfo>> m_Events = new();
        private readonly Dictionary<(Entity Citizen, CustomEventType Event), bool> m_FirstSend = new();
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

            if (!m_Events.TryGetValue(cevent.m_Citizen, out var citizenEvents)) {
                citizenEvents = new Dictionary<CustomEventType, MessageInfo>();
                m_Events.Add(cevent.m_Citizen, citizenEvents);
            }
            m_LastEvent.TryGetValue(cevent.m_Citizen, out EventInfo lastEvent);


            if (!citizenEvents.TryGetValue(cevent.m_EventType, out message)) {
                message = new MessageInfo(cevent, lastEvent);
                citizenEvents.Add(cevent.m_EventType, message);
            } else {
                message.SetEvent(cevent, lastEvent);
            }

            var key = (cevent.m_Citizen, cevent.m_EventType);

            if (!m_FirstSend.TryGetValue(key, out bool firstSend)) {
                firstSend = true;
                m_FirstSend.Add(key, true);
            }

            if (!message.canSend(firstSend))
                return;

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


            } else {
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
            }

            foreach (MessageInfo watcher in citizenEvents.Values) {
                watcher.StoreWatchedEvent(message);
            }

            if (firstSend)
                m_FirstSend[key] = false;

            if (message.CanRemove())
                citizenEvents.Remove(cevent.m_EventType);

            m_LastEvent[cevent.m_Citizen] = message.GetEventInfo();
        }
    }
}
