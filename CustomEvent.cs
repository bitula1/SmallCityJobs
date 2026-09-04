using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod {
    public struct CustomEvent {
        public Entity m_Citizen;
        public CustomEventType m_EventType;
        public FixedString64Bytes m_Param;
    }
}
