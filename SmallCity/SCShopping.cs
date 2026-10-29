using BitulaMod.Components;
using Game.Citizens;
using Game.Economy;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static UnityEngine.InputSystem.InputRemoting;

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
        private SCInfo<SCTravelInfo> m_Travel;
        private SCInfo<SCResourceInfo> m_Resource;
        private SCInfo<SCHouseholdInfo> m_Household;

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
                m_Travel = SCInfo<SCTravelInfo>.Create(ref state),
                m_Resource = SCInfo<SCResourceInfo>.Create(ref state),
                m_Household = SCInfo<SCHouseholdInfo>.Create(ref state),
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

        public bool IsNormalShopping(Entity citizen) {
            return m_Common.IsSmallCityBehaviour(citizen) && m_NormalShopping;
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

        public bool CantBuyResourceIn(Entity citizen, Entity building) {
            return m_Building.Get().IsInBuilding(citizen, building) &&
                m_Travel.Get().TryGetPurpose(citizen, Purpose.Shopping, out TravelPurpose purpose) &&
                m_Building.Get().HasNoResource(building, purpose.m_Resource);
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

        public bool IsValidPayer(Entity citizen, Entity payer, out byte saleFlags) {
            saleFlags = 0;

            if (!IsSmallCityBehaviour(citizen) || !m_NormalShopping)
                return false;

            if (!m_ShoppingStarts.TryGetComponent(citizen, out CmpShoppingStart shopping) ||
                !shopping.m_PurchaseMade)
                return false;

            if (!m_Household.Get().TryGetHousehold(citizen, out Entity household) ||
                household != payer)
                return false;

            if (!m_Resource.Get().HasResourceBuffer(payer))
                return false;

            saleFlags = shopping.m_SaleFlags;
            return true;
        }

        public void SetShoppingStart(Entity citizen, Entity seller, byte saleFlags) {
            if (m_Common.IsSmallCityBehaviour(citizen) && m_NormalShopping) {
                uint startFrame = m_Common.GetSimulationFrame();
                uint lastPossibleFrame = m_Common.GetLastPossibleFrame(citizen);
                CmpShoppingStart component = new CmpShoppingStart {
                    m_StartFrame = startFrame,
                    m_EndFrame = ShoppingEndFrame(citizen, lastPossibleFrame, startFrame),
                    m_PurchaseMade = false,
                    m_SaleFlags = saleFlags
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

        public void PurchaseAtShop(Entity citizen, Entity building, TravelPurpose purpose) {
            if (!IsSmallCityBehaviour(citizen) || !m_NormalShopping)
                return;

            if (!m_ShoppingStarts.TryGetComponent(citizen, out CmpShoppingStart shopping))
                return;

            if (shopping.m_PurchaseMade)
                return;

            if (!m_Building.Get().TryGetSeller(building, purpose.m_Resource, out Entity seller))
                return;

            int available = m_Resource.Get().GetResourceAmount(seller, purpose.m_Resource);
            int amount = math.min(purpose.m_Data, available);

            if (amount <= 0)
                return;

            if (!m_Household.Get().TryGetHousehold(citizen, out Entity household))
                return;

            m_Common.AddCmp(citizen, new ResourceBought {
                m_Payer = household,
                m_Seller = seller,
                m_Resource = purpose.m_Resource,
                m_Amount = amount,
                m_Distance = 0f
            });
            
            shopping.m_PurchaseMade = true;
            m_Common.SetCmp(citizen, shopping);

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

        public void PrintResourceBought(Entity household, Entity seller, Resource resource, int amount, int moneySpent) {
            if (!m_Household.Get().TryGetHouseholdCitizens(household, out var citizens))
                return;

            for (int i = 0; i < citizens.Length; i++) {
                Entity citizen = citizens[i].m_Citizen;

                bool matched = false;

                if (m_Resource.Get().TryGetResourceBought(citizen, out ResourceBought bought)) {
                    matched = bought.m_Payer == household &&
                              bought.m_Seller == seller &&
                              bought.m_Resource == resource &&
                              bought.m_Amount == amount;
                }

                if (!matched) {
                    matched = m_Resource.Get().IsMatchingResourceBuyer(
                        citizen, household, seller, resource, amount
                    );
                }

                if (!matched)
                    continue;

                m_Common.AddParameter(amount);
                m_Common.AddParameter(EconomyUtils.GetNameFixed(resource));
                m_Common.AddParameter(moneySpent);
                m_Common.Send(citizen, CustomEventType.ResourceBought);
                return;
            }
        }

        public void PrintShoppingDebug(Entity citizen, FixedString512Bytes message) {
            m_Common.AddParameter(message);
            m_Common.Send(citizen, CustomEventType.DebugMessage);
        }

        public void Update(ref SystemState state) {
            m_Common.Update(ref state);
            m_ShoppingStarts.Update(ref state);
            m_ShopFound.Update(ref state);
            m_ShopResourceFound.Update(ref state);

            m_Leisure.Update(ref state);
            m_Shopping.Update(ref state);
            m_Building.Update(ref state);
            m_Travel.Update(ref state);
            m_Resource.Update(ref state);
            m_Household.Update(ref state);
        }


       
    }
}
