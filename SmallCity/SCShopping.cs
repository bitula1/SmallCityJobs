using BitulaMod.Components;
using Game.Buildings;
using Game.Citizens;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;

namespace BitulaMod.SmallCity {
    public struct SCShopping : ISCFeature<SCShopping> {
        private SCCommon m_Common;
        private bool m_NormalShopping;
        private int m_ShoppingIntervalMin;
        private int m_ShoppingIntervalMax;
        private bool m_PhysicalVirtualShopping;
        private ComponentLookup<CmpShoppingStart> m_ShoppingStarts;
        private SCInfo<SCLeisureInfo> m_Leisure;
        private SCInfo<SCShoppingInfo> m_Shopping;
        private SCInfo<SCBuildingsInfo> m_Building;
        [ReadOnly]
        public ComponentLookup<CmpShopFound> m_ShopFound;
        [ReadOnly]
        public ComponentLookup<CmpShopResourceFound> m_ShopResourceFound;
        public SCCommon Common {
            get => m_Common;
            set => m_Common = value;
        }
        public SCShopping Create(ref SystemState state, in SCCommon common) {

            return new SCShopping {
                m_Common = common,
                m_NormalShopping = Mod.Settings.NormalShopping,
                m_ShoppingIntervalMin = Mod.Settings.ShoppingIntervalMin,
                m_ShoppingIntervalMax = Mod.Settings.ShoppingIntervalMax,
                m_PhysicalVirtualShopping = Mod.Settings.PhysicalVirtualShopping,
                m_Leisure = SCInfo<SCLeisureInfo>.Create(ref state),
                m_Shopping = SCInfo<SCShoppingInfo>.Create(ref state),
                m_Building = SCInfo<SCBuildingsInfo>.Create(ref state),
                m_ShoppingStarts = state.GetComponentLookup<CmpShoppingStart>(true),
                m_ShopFound = state.GetComponentLookup<CmpShopFound>(true),
                m_ShopResourceFound = state.GetComponentLookup<CmpShopResourceFound>(true),
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
            uint currentFrame = m_Common.GetSimulationFrame();

            return shopping.m_EndFrame != 0 &&
                   shopping.m_StartFrame <= currentFrame &&
                   shopping.m_EndFrame > currentFrame;
        }

        public void SetShopFound(Entity citizen, bool value) {            
            m_Common.AddOrSetCmp(citizen, new CmpShopFound { m_Value = value });
        }

        public void SetShopResourceFound(Entity citizen, bool value) {            
            m_Common.AddOrSetCmp(citizen, new CmpShopResourceFound { m_Value = value });
        }

        public bool IsShopFound(Entity citizen) {
            return m_ShopFound.TryGetComponent(citizen, out var cmp) && cmp.m_Value;
        }

        public bool IsShopResourceFound(Entity citizen) {
            return m_ShopResourceFound.TryGetComponent(citizen, out var cmp) && cmp.m_Value;
        }

        public bool CanShop(Entity citizen, Resource resource) {
            bool canShop = !m_Common.IsSmallCityBehaviour(citizen) ||
                   !m_NormalShopping ||
                   (!m_Leisure.Get().IsWithinLeisureTime(citizen, m_Common.GetSimulationFrame()) &&
                    !m_Shopping.Get().IsWithinShoppingTime(citizen, m_Common.GetSimulationFrame()));

            if (canShop) {
                m_Common.AddParameter(EconomyUtils.GetNameFixed(resource));
                m_Common.Send(citizen, CustomEventType.StartShopResource);
            }
            return canShop;
        }

        public void SetShoppingStart(Entity citizen, Entity seller) {
            if (m_Common.IsSmallCityBehaviour(citizen) && m_NormalShopping) {
                uint startFrame = m_Common.GetSimulationFrame();
                uint lastPossibleFrame = m_Common.GetLastPossibleFrame(citizen);
                CmpShoppingStart component = new CmpShoppingStart {
                    m_StartFrame = m_Common.GetSimulationFrame(),
                    m_EndFrame = ShoppingEndFrame(citizen, lastPossibleFrame, startFrame)
                };
                if (m_ShoppingStarts.HasComponent(citizen))
                    m_Common.SetCmp(citizen, component);
                else
                    m_Common.AddCmp(citizen, component);
            }
            Entity destination = m_Building.Get().GetBuilding(seller);
            m_Common.Send(citizen, CustomEventType.GoToShop, destination);
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

        public bool BuyVirtualGoodsPhysically(Entity citizen) {
            return m_NormalShopping && m_PhysicalVirtualShopping &&
                   m_Common.IsSmallCityBehaviour(citizen);
        }

        public void PrintShoppingIssue(Entity citizen, bool isVirtualResource, bool isTripSuppressed, bool hasAffordableAmount) {
            bool physicalVirtual = isVirtualResource && BuyVirtualGoodsPhysically(citizen);
            if (!hasAffordableAmount)
                m_Common.Send(citizen, CustomEventType.ShopNoMoney);
            else if ((isVirtualResource || isTripSuppressed) && !physicalVirtual)
                m_Common.Send(citizen, CustomEventType.ShopOnline);
        }

        public void PrintNoShoppingPath(Entity citizen, bool staleDestination = false) {
            if (!IsShopFound(citizen))
                m_Common.Send(citizen, CustomEventType.ShopDoesNotExist);
            else if (!IsShopResourceFound(citizen))
                m_Common.Send(citizen, CustomEventType.ShopNoResource);
            else if (staleDestination)
                m_Common.Send(citizen, CustomEventType.ShopDestinationUnavailable);
            else
                m_Common.Send(citizen, CustomEventType.ShopNoPath);
        }

        public void PrintShopDestinationUnavailable(Entity citizen) {
            m_Common.Send(citizen, CustomEventType.ShopDestinationUnavailable);
        }

        public void Update(ref SystemState state) {
            throw new System.NotImplementedException();
        }

        public void PrintShoppingDebug(Entity citizen, FixedString512Bytes message) {
            m_Common.AddParameter(message);
            m_Common.Send(citizen, CustomEventType.DebugMessage);
        }
    }
}
