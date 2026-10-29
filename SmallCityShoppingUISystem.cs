using BitulaMod.Components;
using BitulaMod.SmallCity;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using Game.UI.InGame;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace BitulaMod
{

    enum ShopperStatus {
        None = 0,
        Shopping = 1,
        GoingToShop = 2,
        Idling = 3,
        GoingToHome = 4,
        GoingToLeisure = 5,
        GoingToWork = 6,
        GoingToAnotherShop = 7,
        NotLeisure = 8,
        NotAnotherShopping = 9
    }


    public partial class SmallCityShoppingUISystem : UISystemBase {
        private class Delay {
            public DateTime m_Time;
            public VisualShoppingDelay m_Target;
        }

        enum VisualShoppingDelay {
            None = 0,
            GoingHome = 1,
            GoingToWork = 2,
            GoingToLeisure = 4,
            GoingToAnotherShop = 8,
            NotLeisure = 16,
            NotAnotherShopping = 32,
            NoResource = 64
        }
        private class ShopperInfo {
            public ShopperInfo(Entity citizen) {
                Shopper = citizen;
                delay = new Delay();
            }
            public Entity Shopper;
            public ShopperStatus Status;
            public Delay delay;
            public CitizenAgeKey Age;
            public HouseholdWealthKey Wealth;
            public byte Family;
            public string ShopperHours;
            public int Resource = -1;
            public int Money;
            public void RemoveDelay() {
                delay = null;
            }
            public void RemoveStatusDelay() {
                if (delay == null)
                    return;

                delay.m_Target &=
                    ~(VisualShoppingDelay.GoingHome | VisualShoppingDelay.GoingToWork | VisualShoppingDelay.GoingToAnotherShop | VisualShoppingDelay.GoingToLeisure
                    | VisualShoppingDelay.NotAnotherShopping | VisualShoppingDelay.NotLeisure);

                if (delay.m_Target == 0) {
                    delay = null;
                }
            }
        }

        private readonly Dictionary<Entity, ShopperInfo> m_Shoppers = new();

        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private RawValueBinding m_ShoppersBinding;
        private NameSystem m_NameSystem;
        private EntityQuery m_ShoppingCitizenQuery;
        private SmallCity<SCShopping> m_SmallCityShopping;
        private SCInfo<SCBuildingsInfo> m_BuildingInfo;
        private SCInfo<SCHouseholdInfo> m_HouseholdInfo;
        private ComponentLookup<CurrentBuilding> m_CurrentBuildings;
        private ComponentLookup<Target> m_Targets;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
        private ComponentLookup<ResourceBuyer> m_Shopping;
        private SimulationSystem m_SimulationSystem;
        private EntityQuery m_HappinessParameterQuery;
        private readonly Dictionary<Entity, Entity> m_ShoppingVisitBuilding = new();
        private Entity m_LastSelectedEntity = Entity.Null;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 128;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_ShoppingCitizenQuery = GetEntityQuery( ComponentType.ReadOnly<Citizen>());
            m_CurrentBuildings = GetComponentLookup<CurrentBuilding>(true);
            m_Targets = GetComponentLookup<Target>(true);
            m_PropertyRenters = GetComponentLookup<PropertyRenter>(true);
            m_ShoppersBinding = new RawValueBinding("BitulaMod", "shoppers", BindShoppers);
            m_Shopping = GetComponentLookup<ResourceBuyer>(true);
            m_HappinessParameterQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
            m_BuildingInfo = SCInfo<SCBuildingsInfo>.Create(ref base.CheckedStateRef);
            m_HouseholdInfo = SCInfo<SCHouseholdInfo>.Create(ref base.CheckedStateRef);

            AddBinding(m_ShoppersBinding);
            RequireForUpdate(m_HappinessParameterQuery);
            Mod.log.Info("SmallCityShoppingUISystem created successfully");
        }

        protected override void OnUpdate() {
            base.OnUpdate();
            ref SystemState state = ref CheckedStateRef;            
            Entity selectedEntity = m_SelectedInfoUISystem.selectedEntity;
            
            if (selectedEntity == Entity.Null ||
                !EntityManager.Exists(selectedEntity)) {
                m_ShoppersBinding.Update();
                return;
            }

            m_PropertyRenters.Update(ref state);
            if (selectedEntity != m_LastSelectedEntity) {
                m_Shoppers.Clear();
                m_ShoppingVisitBuilding.Clear();
                m_LastSelectedEntity = selectedEntity;
            }


            m_CurrentBuildings.Update(ref state);
            m_Targets.Update(ref state);
            m_Shopping.Update(this);
            m_SmallCityShopping = SmallCity<SCShopping>.Create(ref state);
            m_BuildingInfo.Update(ref state);
            m_HouseholdInfo.Update(ref state);
            AddShoppers(selectedEntity);
            m_ShoppersBinding.Update();
        }
       
        private void ClearShoppers(Entity selectedEntity) {
            DateTime now = DateTime.UtcNow;

            m_Shoppers.Where(x => {
                if (!EntityManager.Exists(x.Key) || !EntityManager.HasComponent<Citizen>(x.Key) || !EntityManager.HasComponent<HouseholdMember>(x.Key))
                    return true;
                bool valid = isValid(x.Key, selectedEntity);

               if (valid) {
                   return false;
               }

               return (x.Value.delay == null || now >= x.Value.delay.m_Time);
            }).Select(x => x.Key).ToList().ForEach(x => m_Shoppers.Remove(x));
        }


        private void AddShoppers(Entity selectedEntity) {
            List<ShopperInfo> shoppers = new();
            using NativeArray<Entity> citizens = m_ShoppingCitizenQuery.ToEntityArray(Allocator.Temp);
            foreach (Entity citizen in citizens) {
                if (isValid(citizen, selectedEntity) && !m_Shoppers.TryGetValue(citizen, out ShopperInfo shopper)) {
                    shopper = new ShopperInfo(citizen);
                    m_Shoppers.Add(citizen, shopper);
                }
            }
            ClearShoppers(selectedEntity);
            foreach (ShopperInfo shopper in m_Shoppers.Values.OrderBy(s => m_NameSystem.GetRenderedLabelName(s.Shopper), StringComparer.CurrentCultureIgnoreCase)) {
                Entity citizen = shopper.Shopper;
                uint currentFrame = m_SimulationSystem.frameIndex;

                bool hasTravelPurpose = EntityManager.TryGetComponent<TravelPurpose>(citizen, out TravelPurpose travelPurpose);
                

                Entity targetEntity = Entity.Null;

                if (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Shopping &&
                    EntityManager.TryGetComponent<CurrentTransport>(citizen, out CurrentTransport transport) &&
                    m_Targets.TryGetComponent(transport.m_CurrentTransport, out Target target)) {

                    targetEntity = target.m_Target;
                }

                if (m_PropertyRenters.TryGetComponent(targetEntity, out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }                

                bool knownShoppingVisit =  (targetEntity == selectedEntity) 
                    || (m_ShoppingVisitBuilding.TryGetValue(citizen, out Entity shopBuilding) && shopBuilding == selectedEntity);

                bool isInSelectedBuilding = m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity;

                FillUIData(shopper, currentFrame);

                if (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Shopping && targetEntity == selectedEntity && !isInSelectedBuilding) {
                    shopper.Status = ShopperStatus.GoingToShop;
                    m_ShoppingVisitBuilding[citizen] = selectedEntity;
                } else if (IsDelayActive(citizen, selectedEntity, shopper)) {
                    //Handled by IsDelay
                } else if (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Shopping && isInSelectedBuilding) {
                    shopper.Status = ShopperStatus.Shopping;
                    m_ShoppingVisitBuilding[citizen] = selectedEntity;
                } else if (!hasTravelPurpose && knownShoppingVisit && isInSelectedBuilding) {
                    shopper.RemoveStatusDelay();
                    shopper.Status = ShopperStatus.Idling;
                } else {
                    continue;
                }

                shoppers.Add(shopper);
            }            
        }

        private bool isValid(Entity citizen, Entity selectedEntity) {
            if (!EntityManager.Exists(citizen) || !EntityManager.HasComponent<Citizen>(citizen) || !EntityManager.HasComponent<HouseholdMember>(citizen)) {
                return false;
            }

            bool hasTravelPurpose = EntityManager.TryGetComponent<TravelPurpose>(citizen, out TravelPurpose travelPurpose);            

            Entity targetEntity = Entity.Null;

            if (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Shopping &&
                EntityManager.TryGetComponent<CurrentTransport>(citizen, out CurrentTransport transport) &&
                m_Targets.TryGetComponent(transport.m_CurrentTransport, out Target target)) {

                targetEntity = target.m_Target;
            }

            if (m_PropertyRenters.TryGetComponent(targetEntity, out PropertyRenter renter)) {
                targetEntity = renter.m_Property;
            }

            bool knownShoppingVisit = (targetEntity == selectedEntity)
                || (m_ShoppingVisitBuilding.TryGetValue(citizen, out Entity shopBuilding) && shopBuilding == selectedEntity);

            bool isInSelectedBuilding = m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding currentBuilding) &&
                currentBuilding.m_CurrentBuilding == selectedEntity;

            return (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Shopping &&
                    (targetEntity == selectedEntity || isInSelectedBuilding))
                || (!hasTravelPurpose &&
                m_ShoppingVisitBuilding.TryGetValue(citizen, out shopBuilding) &&
                shopBuilding == selectedEntity && isInSelectedBuilding);
        }
        private bool HasTripPurpose(Entity citizen, Purpose purpose) {
            if (!EntityManager.TryGetBuffer<TripNeeded>(
                    citizen,
                    true,
                    out DynamicBuffer<TripNeeded> trips)) {
                return false;
            }

            for (int i = 0; i < trips.Length; i++) {
                if (trips[i].m_Purpose == purpose)
                    return true;
            }

            return false;
        }

        private void SetVisualDelay(ShopperInfo shopper, VisualShoppingDelay target) {
            DateTime now = DateTime.UtcNow;

            if (shopper.delay == null || now >= shopper.delay.m_Time) {
                shopper.delay = new Delay {
                    m_Time = now.AddSeconds(5),
                    m_Target = target
                };
                return;
            }

            if ((shopper.delay.m_Target & target) != 0)
                return;

            shopper.delay.m_Target |= target;
            shopper.delay.m_Time = now.AddSeconds(5);
        }


        private bool IsDelayActive(Entity citizen, Entity selectedEntity, ShopperInfo shopper) {
            shopper.Resource = -1;

            bool liveStatus = false;
            bool isInSelectedBuilding = m_BuildingInfo.Get().IsInBuilding(citizen, selectedEntity);

            if (m_SmallCityShopping.Get().CantBuyResourceIn(citizen, selectedEntity)) {
                SetVisualDelay(shopper, VisualShoppingDelay.NoResource);
            }

            bool hasLeisure = EntityManager.TryGetComponent<Leisure>(
                citizen,
                out Leisure leisure
            );
            Entity targetEntity = Entity.Null;
            if (hasLeisure) {
                targetEntity = leisure.m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(
                        targetEntity,
                        out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }
            }

            if (HasTripPurpose(citizen, Purpose.GoingHome)) {
                shopper.Status = ShopperStatus.GoingToHome;
                SetVisualDelay(shopper, VisualShoppingDelay.GoingHome);
                liveStatus = true;
            } else if (HasTripPurpose(citizen, Purpose.GoingToWork)) {
                shopper.Status = ShopperStatus.GoingToWork;
                SetVisualDelay(shopper, VisualShoppingDelay.GoingToWork);
                liveStatus = true;
            } else if (HasTripPurpose(citizen, Purpose.Leisure)) {
                shopper.Status = ShopperStatus.GoingToLeisure;
                SetVisualDelay(shopper, VisualShoppingDelay.GoingToLeisure);
                liveStatus = true;
            } else if (GoingToOtherShop(citizen, selectedEntity)) {
                shopper.Status = ShopperStatus.GoingToAnotherShop;
                SetVisualDelay(shopper, VisualShoppingDelay.GoingToAnotherShop);
                liveStatus = true;
            } else if (m_Shopping.HasComponent(citizen) && isInSelectedBuilding && !HasTripPurpose(citizen, Purpose.Shopping)) {
                shopper.Status = ShopperStatus.NotAnotherShopping;
                SetVisualDelay(shopper, VisualShoppingDelay.NotAnotherShopping);
                liveStatus = true;
            } else if (hasLeisure && isInSelectedBuilding && targetEntity != selectedEntity) {
                shopper.Status = ShopperStatus.NotLeisure;
                SetVisualDelay(shopper, VisualShoppingDelay.NotLeisure);
                liveStatus = true;
            }

            if (shopper.delay == null || DateTime.UtcNow >= shopper.delay.m_Time) {
                shopper.RemoveDelay();
                return liveStatus;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.NoResource) != 0) {
                shopper.Resource = 0;
            }

            // Current live status has priority over an older delayed status.
            if (liveStatus) {
                return true;
            }



            if ((shopper.delay.m_Target & VisualShoppingDelay.GoingHome) != 0) {
                shopper.Status = ShopperStatus.GoingToHome;
                return true;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.GoingToWork) != 0) {
                shopper.Status = ShopperStatus.GoingToWork;
                return true;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.GoingToAnotherShop) != 0) {
                shopper.Status = ShopperStatus.GoingToAnotherShop;
                return true;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.GoingToLeisure) != 0) {
                shopper.Status = ShopperStatus.GoingToLeisure;
                return true;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.NotLeisure) != 0) {
                shopper.Status = ShopperStatus.NotLeisure;
                return true;
            }

            if ((shopper.delay.m_Target & VisualShoppingDelay.NotAnotherShopping) != 0) {
                shopper.Status = ShopperStatus.NotAnotherShopping;
                return true;
            }

            return false;
        }

        private bool GoingToOtherShop(Entity citizen, Entity selectedEntity) {
            if (!EntityManager.TryGetBuffer<TripNeeded>(
                    citizen,
                    true,
                    out DynamicBuffer<TripNeeded> trips)) {
                return false;
            }

            for (int i = 0; i < trips.Length; i++) {
                if (trips[i].m_Purpose != Purpose.Shopping)
                    continue;

                Entity target = trips[i].m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(target, out PropertyRenter renter))
                    target = renter.m_Property;

                if (target != selectedEntity)
                    return true;
            }

            return false;
        }

        private void FillUIData(ShopperInfo shopper, uint currentFrame) {

            Entity citizen = shopper.Shopper;
            HouseholdMember householdMember = EntityManager.GetComponentData<HouseholdMember>(citizen);
            bool hasLeisure = EntityManager.TryGetComponent<Leisure>(citizen, out Leisure leisure);            
            shopper.Wealth = CitizenUIUtils.GetHouseholdWealth(EntityManager, householdMember.m_Household,
                m_HappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>());
            shopper.Age = CitizenUIUtils.GetAge(EntityManager, citizen);
            if (EntityManager.TryGetBuffer<HouseholdCitizen>(householdMember.m_Household, true, out var members)) {
                shopper.Family = (byte)math.clamp(members.Length, 1, 5);
            }
            bool hasShopping = EntityManager.HasComponent<CmpShoppingStart>(citizen);
            if (m_SmallCityShopping.Get().IsSmallCityBehaviour(citizen) && hasShopping) {
                uint endFrame = m_SmallCityShopping.Get().GetShoppingEndFrame(citizen);
                long remainingFrames = Math.Max(0, (long)endFrame - currentFrame);

                if (remainingFrames > 0) {
                    float remainingHours = remainingFrames * 24f / 262144f;

                    int hours = (int)remainingHours;
                    int minutes = (int)((remainingHours - hours) * 60f);

                    shopper.ShopperHours = $"{hours:00}:{minutes:00}";
                } else {
                    shopper.ShopperHours = "00:00";
                }
            }
            shopper.Money = m_HouseholdInfo.Get().GetMoney(citizen);
        }

        private void BindShoppers(IJsonWriter writer) {
            writer.ArrayBegin((uint)m_Shoppers.Count);

            foreach (ShopperInfo shopper in m_Shoppers.Values.OrderBy(s => m_NameSystem.GetRenderedLabelName(s.Shopper),
                StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Shopper.Index)) {
                writer.TypeBegin("Shopper");

                writer.PropertyName("entity");
                writer.TypeBegin("Entity");

                writer.PropertyName("index");
                writer.Write(shopper.Shopper.Index);

                writer.PropertyName("version");
                writer.Write(shopper.Shopper.Version);

                writer.TypeEnd();

                writer.PropertyName("name");
                m_NameSystem.BindName(writer, shopper.Shopper);

                writer.PropertyName("shopperHours");
                writer.Write(shopper.ShopperHours ?? "");

                writer.PropertyName("status");
                writer.Write((int)shopper.Status);

                writer.PropertyName("family");
                writer.Write((int)shopper.Family);

                writer.PropertyName("age");
                writer.Write((int)shopper.Age);

                writer.PropertyName("wealth");
                writer.Write((int)shopper.Wealth);

                writer.PropertyName("resource");
                writer.Write(shopper.Resource);

                writer.PropertyName("money");
                writer.Write(shopper.Money);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
