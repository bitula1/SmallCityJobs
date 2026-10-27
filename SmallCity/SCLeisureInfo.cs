using BitulaMod.Components;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCLeisureInfo : ISCInfo<SCLeisureInfo> {
        [ReadOnly]
        private ComponentLookup<CmpLeisureStart> m_LeisureStarts;

        public SCLeisureInfo Create(ref SystemState state) {
            return new SCLeisureInfo {
                m_LeisureStarts = state.GetComponentLookup<CmpLeisureStart>(true),
            };
        }

        public bool HasLeisureInterval(Entity citizen) {
            return m_LeisureStarts.HasComponent(citizen);
        }

        public uint GetLeisureStartFrame(Entity citizen) {
            return HasLeisureInterval(citizen) ? m_LeisureStarts[citizen].m_StartFrame : 0;
        }

        public uint GetLeisureEndFrame(Entity citizen) {
            return HasLeisureInterval(citizen) ? m_LeisureStarts[citizen].m_EndFrame : 0;
        }

        public bool IsWithinLeisureTime(Entity citizen, uint currentFrame) {
            if (!HasLeisureInterval(citizen))
                return false;

            uint startFrame = GetLeisureStartFrame(citizen);
            uint endFrame = GetLeisureEndFrame(citizen);

            return endFrame > startFrame &&
                   currentFrame >= startFrame &&
                   currentFrame < endFrame;
        }

        public void Update(ref SystemState state) {
            m_LeisureStarts.Update(ref state);
        }
    }
}