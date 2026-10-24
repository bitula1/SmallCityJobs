using BitulaMod.Components;
using Game.Citizens;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCShopping : ISCFeature<SCShopping> {
        private SCCommon m_Common;
        private EntityCommandBuffer.ParallelWriter m_CommandBuffer;
        private bool m_NormalShopping;
        private int m_ShoppingIntervalMin;
        private int m_ShoppingIntervalMax;
        private ComponentLookup<CmpShoppingStart> m_ShoppingStarts;
        public SCShopping Create(ref SystemState state, in SCCommon common) {

            return new SCShopping {
                m_CommandBuffer = common.CommandBuffer,
                m_Common = common,
                m_NormalShopping = Mod.Settings.NormalShopping,
                m_ShoppingIntervalMin = Mod.Settings.ShoppingIntervalMin,
                m_ShoppingIntervalMax = Mod.Settings.ShoppingIntervalMax,

                m_ShoppingStarts = state.GetComponentLookup<CmpShoppingStart>(true),
            };
        }

        public SCShopping Create() {
            return new SCShopping {
                m_NormalShopping = Mod.Settings.NormalShopping,
                m_ShoppingIntervalMin = Mod.Settings.ShoppingIntervalMin,
                m_ShoppingIntervalMax = Mod.Settings.ShoppingIntervalMax,
            };
        }

        public bool IsSmallCityBehaviour(Entity citizen) {
            return m_Common.IsSmallCityBehaviour(citizen);
        }

        public bool HasShoppingStart(Entity citizen) {
            return m_ShoppingStarts.HasComponent(citizen);
        }

        public bool HasShoppingSchedule(Entity citizenEntity) {
            if (citizenEntity == Entity.Null || !m_ShoppingStarts.HasComponent(citizenEntity) 
                || !m_Common.IsSmallCityBehaviour(citizenEntity) || !m_NormalShopping)
                return false;

            CmpShoppingStart shopping = m_ShoppingStarts[citizenEntity];

            return shopping.m_EndFrame != 0 &&
                   shopping.m_StartFrame / SCCommon.kFramesPerDay ==
                   m_Common.GetSimulationFrame() / SCCommon.kFramesPerDay;
        }

        public void SetShoppingStart(Entity citizen, uint startFrame) {
            uint lastPossibleFrame = m_Common.GetLastPossibleFrame(citizen);
            CmpShoppingStart component = new CmpShoppingStart {
                m_StartFrame = startFrame,
                m_EndFrame = ShoppingEndFrame(citizen, lastPossibleFrame, startFrame)
            };
            if (m_ShoppingStarts.HasComponent(citizen))
                m_CommandBuffer.SetComponent(citizen.Index, citizen, component);
            else
                m_CommandBuffer.AddComponent(citizen.Index, citizen, component);
        }

        public uint GetShoppingStartFrame(Entity citizen) {
            if (!m_ShoppingStarts.HasComponent(citizen))
                return 0;

            return m_ShoppingStarts[citizen].m_StartFrame;
        }

        public uint GetShoppingEndFrame(Entity citizen) {
            if (!m_ShoppingStarts.HasComponent(citizen))
                return 0;

            return m_ShoppingStarts[citizen].m_EndFrame;
        }

        private uint ShoppingEndFrame(Entity citizen, uint lastPossibleFrame, uint startFrame) {
            if (startFrame == 0 || lastPossibleFrame <= startFrame)
                return 0;
            Citizen citizenData = m_Common.GetCitizenData(citizen);
            uint day = startFrame / SCCommon.kFramesPerDay;

            float percent = Unity.Mathematics.Random.CreateFromIndex(
                (uint)citizenData.m_PseudoRandom + 30000u + day
            ).NextFloat(m_ShoppingIntervalMin / 100f, m_ShoppingIntervalMax / 100f);

            uint duration = lastPossibleFrame - startFrame;
            uint endFrame = startFrame + (uint)(duration * percent);

            return endFrame;
        }

        public uint ShoppingEndFrame(Entity citizen, uint lastPossibleFrame) {
            Citizen citizenData = m_Common.GetCitizenData(citizen);
            uint startFrame = GetShoppingStartFrame(citizen);
            return ShoppingEndFrame(citizen, lastPossibleFrame, startFrame);
        }

        public uint GetLasePossibleFrame(Entity citizen) {
            return m_Common.GetLastPossibleFrame(citizen);
        }

        public bool IsEndOfShopping(Entity citizen) {
            if (!m_Common.IsSmallCityBehaviour(citizen) || !m_NormalShopping)
                return true;

            if (!m_ShoppingStarts.HasComponent(citizen))
                return true;

            Citizen citizenData = m_Common.GetCitizenData(citizen);
            uint endFrame = GetShoppingEndFrame(citizen);

            if (endFrame == 0)
                return true;

            return m_Common.GetSimulationFrame() >= endFrame;
        }
    }
}
