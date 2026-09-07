using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod {
    public struct CustomEvent {
        public const byte IgnoreParameterInFilter = 1;
        public const byte WatchEvent = 2;
        public Entity m_Citizen;
        public CustomEventType m_EventType;
        public CustomEventType m_WatchedEventType;
        public FixedString64Bytes m_Param;
        public byte m_Hint;
    }
}
