using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod {
    public enum EventHint {
        None = 0,
        WatchEvent = 1,
        CounterWatchEvent = 2,
        IgnoreParameters = 4,
        DirectPreviousMessage = 8,
        SendThenWatchEvent = 16
    }
    public struct CustomEvent {
        public Entity m_Citizen;
        public CustomEventType m_EventType;
        public FixedList128Bytes<CustomEventType> m_WatchedEventTypes;
        public FixedString64Bytes m_Param;
        public EventHint m_Hint;
        public EventHint m_WatchHint;
        public Entity m_Target;
    }
}
