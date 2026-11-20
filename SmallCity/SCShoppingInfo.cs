using BitulaMod.Components;
using Game.Companies;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCShoppingInfo : ISCInfo<SCShoppingInfo> {
        [ReadOnly]
        private ComponentLookup<CmpShoppingStart> m_ShoppingStarts;

        [ReadOnly]
        private ComponentLookup<ResourceBuyer> m_ResourceBuyers;

        public SCShoppingInfo Create(ref SystemState state) {
            return new SCShoppingInfo {
                m_ShoppingStarts = state.GetComponentLookup<CmpShoppingStart>(true),
                m_ResourceBuyers = state.GetComponentLookup<ResourceBuyer>(true),
            };
        }

        public bool HasShoppingInterval(Entity citizen) {
            return m_ShoppingStarts.HasComponent(citizen);
        }

        public bool HasResourceBuyer(Entity citizen) {
            return m_ResourceBuyers.HasComponent(citizen);
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
            m_ResourceBuyers.Update(ref state);
        }
    }
}