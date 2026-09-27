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
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Game.Simulation.CountHouseholdDataSystem;

namespace BitulaMod {
    enum LeisureStatus {
        None = 0,
        Leisuring = 1,
        GoingToLeisure = 2,
    }

    public partial class SmallCityLeisureUISystem : UISystemBase {
        private struct CustomerInfo {
            public Entity Customer;
            public LeisureStatus Status;
            public byte LeisureDesire;
            public CitizenAgeKey Age;
            public HouseholdWealthKey Wealth;
        }

        private readonly List<CustomerInfo> m_Customers = new();

        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private RawValueBinding m_CustomersBinding;
        private NameSystem m_NameSystem;
        private EntityQuery m_LeisureCitizenQuery;
        private EntityQuery m_HappinessParameterQuery;
        private ComponentLookup<CurrentBuilding> m_CurrentBuildings;
        private ComponentLookup<Target> m_Targets;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;        
        private ComponentLookup<SmallCityJobsComponent> m_UseSCJ;
        private ComponentLookup<DesiredLeisureTypeComponent> m_Desire;
        private EntityQuery m_GoingToLeisureQuery;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 64;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_LeisureCitizenQuery = GetEntityQuery( ComponentType.ReadOnly<Citizen>(),  ComponentType.ReadOnly<Leisure>());

            m_CurrentBuildings = GetComponentLookup<CurrentBuilding>(true);
            m_Targets = GetComponentLookup<Target>(true);
            m_PropertyRenters = GetComponentLookup<PropertyRenter>(true);

            m_CustomersBinding = new RawValueBinding("BitulaMod", "customers", BindCustomers);
            m_GoingToLeisureQuery = GetEntityQuery(
                 ComponentType.ReadOnly<Citizen>(),
                 ComponentType.ReadOnly<TravelPurpose>()
             );
            m_HappinessParameterQuery = GetEntityQuery( ComponentType.ReadOnly<CitizenHappinessParameterData>());
            m_UseSCJ = GetComponentLookup<SmallCityJobsComponent>(true);
            m_Desire = GetComponentLookup<DesiredLeisureTypeComponent>(true);


            AddBinding(m_CustomersBinding);
            RequireForUpdate(m_HappinessParameterQuery);
            Mod.log.Info("SmallCityLeisureUISystem created successfully");
        }

        protected override void OnUpdate() {
            base.OnUpdate();

            m_Customers.Clear();

            Entity selectedEntity = m_SelectedInfoUISystem.selectedEntity;

            if (selectedEntity == Entity.Null ||
                !EntityManager.Exists(selectedEntity)) {
                m_CustomersBinding.Update();
                return;
            }

            ref SystemState state = ref CheckedStateRef;

            m_CurrentBuildings.Update(ref state);
            m_Targets.Update(ref state);
            m_PropertyRenters.Update(ref state);
            m_UseSCJ.Update(ref state);
            m_Desire.Update(ref state);

            AddCustomers(selectedEntity, SmallCityJobs.Create());

            m_CustomersBinding.Update();
        }

        private void AddCustomers(Entity selectedEntity, SmallCityJobs scj) {
            using NativeArray<Entity> citizens = m_LeisureCitizenQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity citizen in citizens) {
                if (!EntityManager.TryGetComponent<Leisure>(
                        citizen,
                        out Leisure leisure)) {
                    continue;
                }
                bool useSCJ = m_UseSCJ.TryGetComponent(citizen, out SmallCityJobsComponent scjComponent) && scjComponent.m_UseSmallCityBehaviour;

                CustomerInfo customerInfo = new CustomerInfo {
                    Customer = citizen
                };

                bool addCustomer = false;
                HouseholdMember householdMember = EntityManager.GetComponentData<HouseholdMember>(citizen);

                customerInfo.Wealth = CitizenUIUtils.GetHouseholdWealth( EntityManager, householdMember.m_Household, 
                    m_HappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>());
                customerInfo.Age = CitizenUIUtils.GetAge(EntityManager, citizen);

                if (useSCJ) {
                    Citizen citizenData = EntityManager.GetComponentData<Citizen>(citizen);
                    customerInfo.LeisureDesire = scj.GetLeisureDesireLevel(citizenData);
                } else if (m_Desire.TryGetComponent( citizen, out DesiredLeisureTypeComponent desireComponent)) {
                    customerInfo.LeisureDesire = desireComponent.m_Desire;
                }

                // Already at the selected leisure location
                Entity targetEntity = leisure.m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(
                        targetEntity,
                        out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }

                // Already at the selected leisure location
                if (m_CurrentBuildings.TryGetComponent(
                        citizen,
                        out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity &&
                    targetEntity == selectedEntity) {

                    customerInfo.Status = LeisureStatus.Leisuring;
                    addCustomer = true;
                } else if (EntityManager.TryGetComponent<TravelPurpose>(
                               citizen,
                               out TravelPurpose travelPurpose) &&
                           travelPurpose.m_Purpose == Purpose.Leisure &&
                           targetEntity == selectedEntity) {

                    customerInfo.Status = LeisureStatus.GoingToLeisure;
                    addCustomer = true;
                }

                if (addCustomer) {
                    m_Customers.Add(customerInfo);
                }
            }
        }

        private void BindCustomers(IJsonWriter writer) {
            writer.ArrayBegin((uint)m_Customers.Count);

            foreach (CustomerInfo customer in m_Customers) {
                writer.TypeBegin("Customer");

                writer.PropertyName("entity");
                writer.TypeBegin("Entity");

                writer.PropertyName("index");
                writer.Write(customer.Customer.Index);

                writer.PropertyName("version");
                writer.Write(customer.Customer.Version);

                writer.TypeEnd();

                writer.PropertyName("name");
                m_NameSystem.BindName(writer, customer.Customer);

                writer.PropertyName("status");
                writer.Write((int)customer.Status);

                writer.PropertyName("desire");
                writer.Write((int)customer.LeisureDesire);

                writer.PropertyName("age");
                writer.Write((int)customer.Age);

                writer.PropertyName("wealth");
                writer.Write((int)customer.Wealth);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
