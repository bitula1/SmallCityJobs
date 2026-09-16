using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BitulaMod {
    public struct SmallCityJobsComponent : IComponentData {
        public bool m_UseSmallCityBehaviour;
        public bool m_FoundHigherJob;
        public bool m_FoundCloserJob;
        public SmallCityJobsPhase m_phase;
    }
}
