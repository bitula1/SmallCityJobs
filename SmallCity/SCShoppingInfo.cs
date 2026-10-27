using BitulaMod.Components;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCShoppingInfo : ISCInfo<SCShoppingInfo> {
        [ReadOnly]
        private ComponentLookup<CmpShoppingStart> m_ShoppingStarts;

        public SCShoppingInfo Create(ref SystemState state) {
            return new SCShoppingInfo {
                m_ShoppingStarts = state.GetComponentLookup<CmpShoppingStart>(true),
            };
        }

        public bool HasShoppingInterval(Entity citizen) {
            return m_ShoppingStarts.HasComponent(citizen);
        }

        public uint GetShoppingStartFrame(Entity citizen) {
            return HasShoppingInterval(citizen) ? m_ShoppingStarts[citizen].m_StartFrame : 0;
        }

        public uint GetShoppingEndFrame(Entity citizen) {
            return HasShoppingInterval(citizen) ? m_ShoppingStarts[citizen].m_EndFrame : 0;
        }

        public bool IsWithinShoppingTime(Entity citizen, uint currentFrame) {
            if (!HasShoppingInterval(citizen))
                return false;

            uint startFrame = GetShoppingStartFrame(citizen);
            uint endFrame = GetShoppingEndFrame(citizen);

            return endFrame > startFrame &&
                   currentFrame >= startFrame &&
                   currentFrame < endFrame;
        }

        public void Update(ref SystemState state) {
            m_ShoppingStarts.Update(ref state);
        }
    }
}