using Colossal.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using Game.UI.InGame;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

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
        }

        private readonly List<CustomerInfo> m_Customers = new();

        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private RawValueBinding m_CustomersBinding;
        private NameSystem m_NameSystem;
        private EntityQuery m_LeisureCitizenQuery;
        private ComponentLookup<CurrentBuilding> m_CurrentBuildings;
        private ComponentLookup<Target> m_Targets;
        private ComponentLookup<PropertyRenter> m_PropertyRenters;
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


            AddBinding(m_CustomersBinding);

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

            AddCustomers(selectedEntity);

            m_CustomersBinding.Update();
        }

        private void AddCustomers(Entity selectedEntity) {
            using NativeArray<Entity> citizens =
                m_LeisureCitizenQuery.ToEntityArray(Allocator.Temp);

            // Citizens already at the selected leisure location
            foreach (Entity citizen in citizens) {
                if (m_CurrentBuildings.TryGetComponent(
                        citizen,
                        out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity) {

                    m_Customers.Add(new CustomerInfo {
                        Customer = citizen,
                        Status = LeisureStatus.Leisuring
                    });
                }
            }

            using NativeArray<Entity> travelingCitizens =
                m_GoingToLeisureQuery.ToEntityArray(Allocator.Temp);

            // Citizens travelling to the selected leisure location
            foreach (Entity citizen in travelingCitizens) {
                TravelPurpose travelPurpose =
                    EntityManager.GetComponentData<TravelPurpose>(citizen);

                if (travelPurpose.m_Purpose != Purpose.Leisure) {
                    continue;
                }

                // Already there -> handled above as Leisuring
                if (m_CurrentBuildings.TryGetComponent(
                        citizen,
                        out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity) {
                    continue;
                }

                if (!EntityManager.TryGetComponent<Leisure>(
                        citizen,
                        out Leisure leisure)) {
                    continue;
                }

                Entity targetEntity = leisure.m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(
                        targetEntity,
                        out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }

                if (targetEntity == selectedEntity) {
                    m_Customers.Add(new CustomerInfo {
                        Customer = citizen,
                        Status = LeisureStatus.GoingToLeisure
                    });
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

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
