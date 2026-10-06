using Game.SceneFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

namespace BitulaMod {
    public class EventInfo {

        public EventInfo(CustomEventType cevent) {
            m_Event = cevent;            
        }
        public EventInfo(CustomEvent cevent ) {
            m_Event = cevent.m_EventType;
            string[] parameters = cevent.m_Param.ToString().Split(',');
            string key = $"BitulaMod.LIFEPATH_LINK_{cevent.m_EventType}";
            if (!GameManager.instance.localizationManager.activeDictionary.TryGetValue(key, out string template)) {
                template = key;
            }
            

            if (parameters.Length > 0)
                m_ParameterText = string.Format(template, parameters);
            else
                m_ParameterText = template;
            m_EventText = (cevent.m_Hint & EventHint.IgnoreParameters) != 0
                    ? cevent.m_EventType.ToString()
                    : $"{cevent.m_EventType}:{m_ParameterText}";
        }
        private CustomEventType m_Event;
        private String m_EventText;
        private String m_ParameterText;
        private EventHint m_Hints;
        private EventHint m_WatchHints;
        public CustomEventType GetEvent() {
            return m_Event;
        }
        public void AddHint(EventHint hint) {
            m_Hints |= hint;
        }

        public void AddWatchHint(EventHint hint) {
            m_WatchHints |= hint;
        }

        public bool HasHint(EventHint hint) {
            return (m_Hints & hint) != 0;
        }

        public bool HasWatchHint(EventHint hint) {
            return (m_WatchHints & hint) != 0;
        }

        public string GetEventText() {
            return m_EventText;
        }

        public string GetParameterText() {
            return m_ParameterText;
        }
    }
    public class MessageInfo {
        private EventInfo m_Event;
        private EventInfo m_LastEvent;
        private readonly List<EventInfo> m_EventsToWatch = new();
        private readonly List<EventInfo> m_WatchedEvents = new();
        public MessageInfo(CustomEvent cevent) {
            SetEvent(cevent);
        }

        public void SetEvent(CustomEvent cevent) {
            m_Event = new EventInfo(cevent);

            m_EventsToWatch.Clear();

            setSendIfWatched(
                cevent.m_WatchedEventTypes,
                cevent.m_Hint &
                    (EventHint.WatchEvent | EventHint.CounterWatchEvent));

            m_Event.AddHint(cevent.m_Hint);
            m_Event.AddWatchHint(cevent.m_WatchHint);
        }

        public CustomEventType GetEvent() {
            return m_Event.GetEvent();
        }

        public void SetLastEvent(MessageInfo message) {
            m_LastEvent = message.m_Event;
        }

        private EventInfo AddEventInfo(CustomEventType cevent) {
            foreach (EventInfo eventInfo in m_EventsToWatch) {
                if (eventInfo.GetEvent() == cevent)
                    return eventInfo;
            }

            EventInfo newEventInfo = new(cevent);
            m_EventsToWatch.Add(newEventInfo);
            return newEventInfo;
        }

        public void setSendIfWatched(
                FixedList128Bytes<CustomEventType> events,
                EventHint hint) {

            foreach (CustomEventType cevent in events) {
                EventInfo eventInfo = AddEventInfo(cevent);
                eventInfo.AddHint(hint);
            }

            m_Event.AddHint(hint);
        }

        public bool HasHint(EventHint hint) {
            return m_Event.HasHint(hint);
        }

        public bool HasEventsToWatch() {
            return m_EventsToWatch.Count > 0;
        }

        public bool HasEventsToWatch(EventHint hint) {
            foreach (EventInfo eventInfo in m_EventsToWatch) {
                if (eventInfo.HasHint(hint))
                    return true;
            }

            return false;
        }

        public bool HasEventsToWatch(
                CustomEventType eventType,
                EventHint hint) {

            foreach (EventInfo eventInfo in m_EventsToWatch) {
                if (eventInfo.GetEvent() == eventType &&
                    eventInfo.HasHint(hint)) {
                    return true;
                }
            }

            return false;
        }

        public bool HasLastEvent() {
            return m_LastEvent != null;
        }

        public EventInfo GetLastEventInfo() {
            return m_LastEvent;
        }

        public CustomEventType GetLastEvent() {
            return m_LastEvent.GetEvent();
        }

        public void StoreCurrentEvent() {
            m_LastEvent = m_Event;

            if (m_Event.HasWatchHint(
                    EventHint.WatchEvent | EventHint.CounterWatchEvent)) {
                m_WatchedEvents.Add(m_Event);
            }
        }

        public bool LastEventMatchesWatch(EventHint hint) {
            if (m_LastEvent == null ||
                !m_LastEvent.HasWatchHint(hint))
                return false;

            foreach (EventInfo eventInfo in m_EventsToWatch) {
                if (eventInfo.GetEvent() == m_LastEvent.GetEvent() &&
                    eventInfo.HasHint(hint)) {
                    return true;
                }
            }

            return false;
        }

        public bool HasMatchingWatchedEvent(EventHint hint) {
            foreach (EventInfo eventToWatch in m_EventsToWatch) {
                if (!eventToWatch.HasHint(hint))
                    continue;

                foreach (EventInfo watchedEvent in m_WatchedEvents) {
                    if (watchedEvent.GetEvent() == eventToWatch.GetEvent() &&
                        watchedEvent.HasWatchHint(hint)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public string GetEventText() {
            return m_Event.GetEventText();
        }

        public string GetLastEventText() {
            return m_LastEvent?.GetEventText() ?? string.Empty;
        }

        public string GetParameterText() {
            return m_Event.GetParameterText();
        }

        public void RemoveEventsWatched() {
            for (int i = m_WatchedEvents.Count - 1; i >= 0; i--) {
                EventInfo watchedEvent = m_WatchedEvents[i];

                foreach (EventInfo eventToWatch in m_EventsToWatch) {
                    if (eventToWatch.GetEvent() == watchedEvent.GetEvent() &&
                        ((eventToWatch.HasHint(EventHint.WatchEvent) &&
                          watchedEvent.HasWatchHint(EventHint.WatchEvent)) ||
                         (eventToWatch.HasHint(EventHint.CounterWatchEvent) &&
                          watchedEvent.HasWatchHint(EventHint.CounterWatchEvent)))) {

                        m_WatchedEvents.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}
