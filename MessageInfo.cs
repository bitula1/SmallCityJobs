using Game.SceneFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using static UnityEngine.InputSystem.InputRemoting;

namespace BitulaMod {
    public class EventInfo {
        private CustomEventType m_Event;
        private String m_EventText;
        private String m_ParameterText;
        private EventHint m_Hints;
        private EventHint m_WatchHints;

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
        public override bool Equals(object obj) {
            return obj is EventInfo other &&
                   m_Event == other.m_Event;
        }

        public override int GetHashCode() {
            return m_Event.GetHashCode();
        }
        public CustomEventType GetEvent() {
            return m_Event;
        }
        public void AddHint(EventHint hint) {
            m_Hints |= hint;
        }

        public void ReplaceHint(EventHint source, EventHint target) {
            m_Hints &= ~source;
            m_Hints |= target;
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
        private readonly HashSet<EventInfo> m_EventsToWatch = new();
        private readonly HashSet<EventInfo> m_WatchedEvents = new();
        public MessageInfo(CustomEvent currentEvent, EventInfo lastEvent) {
            SetEvent(currentEvent, lastEvent);
        }

        public void SetEvent(CustomEvent currentEvent, EventInfo lastEvent) {
            m_Event = new EventInfo(currentEvent);
            m_LastEvent = lastEvent;
            m_EventsToWatch.Clear();
            setSendIfWatched(currentEvent.m_WatchedEventTypes, currentEvent.m_Hint & (EventHint.WatchEvent | EventHint.CounterWatchEvent | EventHint.SendThenWatchEvent));
            m_Event.AddHint(currentEvent.m_Hint);
            m_Event.AddWatchHint(currentEvent.m_WatchHint);
        }

        public void StoreWatchedEvent(MessageInfo message) {
            foreach (EventInfo eventToWatch in m_EventsToWatch) {
                if (eventToWatch.GetEvent() == message.m_Event.GetEvent()) {
                    m_WatchedEvents.Add(message.m_Event);
                    return;
                }
            }
        }

        public void ReplaceHint(EventHint source, EventHint target) {
            m_Event.ReplaceHint(source, target);

            foreach (EventInfo eventInfo in m_EventsToWatch) {
                if (eventInfo.HasHint(source))
                    eventInfo.ReplaceHint(source, target);
            }
        }

        public bool canSend(bool firstSend) {
            if (!firstSend && HasHint(EventHint.SendThenWatchEvent))
                ReplaceHint(EventHint.SendThenWatchEvent, EventHint.WatchEvent);

            if (HasEventsToWatch()) {
                if (HasHint(EventHint.CounterWatchEvent)) {
                    if (HasHint(EventHint.DirectPreviousMessage)) {
                        if (LastEventMatchesWatch(EventHint.CounterWatchEvent))
                            return false;
                    } else if (HasMatchingWatchedEvent(EventHint.CounterWatchEvent)) {
                        return false;
                    }
                }

                if (HasHint(EventHint.WatchEvent) &&
                    !HasMatchingWatchedEvent(EventHint.WatchEvent)) {
                    return false;
                }
            }

            return true;
        }

        public bool CanRemove() { 
            return !HasHint(EventHint.SendThenWatchEvent); 
        }

        public CustomEventType GetEvent() {
            return m_Event.GetEvent();
        }

        public EventInfo GetEventInfo() {
            return m_Event;
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
    }
}
