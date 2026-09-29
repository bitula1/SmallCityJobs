using Colossal.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Companies;
using Game.Economy;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.UI;
using Game.UI.InGame;
using System;
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
        Idling = 3,
        GoingToHome = 4 
    }

    public partial class SmallCityLeisureUISystem : UISystemBase {
        private struct CustomerInfo {
            public Entity Customer;
            public LeisureStatus Status;
            public byte LeisureDesire;
            public CitizenAgeKey Age;
            public HouseholdWealthKey Wealth;
            public int Resource;
            public bool NoService;
            public string LeisureHours;
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
        private ComponentLookup<PrefabRef> m_PrefabRefs;
        private ComponentLookup<IndustrialProcessData> m_IndustrialProcesses;
        private ComponentLookup<ServiceAvailable> m_ServiceAvailables;
        private BufferLookup<Resources> m_Resources;
        private EntityQuery m_GoingToLeisureQuery;
        private readonly Dictionary<Entity, DateTime> m_GoingHomeUntil = new();
        private readonly List<CustomerInfo> customers = new();
        private readonly Dictionary<Entity, Entity> m_LeisureVisitBuilding = new();

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 128;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_LeisureCitizenQuery = GetEntityQuery( ComponentType.ReadOnly<Citizen>());

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
            m_PrefabRefs = GetComponentLookup<PrefabRef>(true);
            m_IndustrialProcesses = GetComponentLookup<IndustrialProcessData>(true);
            m_Resources = GetBufferLookup<Resources>(true);
            m_ServiceAvailables = GetComponentLookup<ServiceAvailable>(true);


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
            m_PrefabRefs.Update(this);
            m_IndustrialProcesses.Update(this);
            m_Resources.Update(this);
            m_ServiceAvailables.Update(this);
            m_Customers.Clear();
            AddCustomers(selectedEntity, SmallCityJobs.Create());
            m_CustomersBinding.Update();
        }
        private static string FormatTime(float normalizedTime) {
            int totalMinutes =
                (int)math.round(math.frac(normalizedTime) * 1440f);

            totalMinutes %= 1440;

            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            return $"{hour:00}:{minute:00}";
        }


        private void AddCustomers(Entity selectedEntity, SmallCityJobs scj) {
            m_Customers.Clear();
            List<CustomerInfo> customers = new();
            using NativeArray<Entity> citizens = m_LeisureCitizenQuery.ToEntityArray(Allocator.Temp);
            SimulationSystem simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem.frameIndex;
            foreach (Entity citizen in citizens) {
                CustomerInfo customerInfo = new CustomerInfo {
                    Customer = citizen,
                    LeisureHours = "",
                    Resource = -1
                };
                bool hasLeisure = EntityManager.TryGetComponent<Leisure>(citizen, out Leisure leisure);
                bool hasTravelPurpose = EntityManager.TryGetComponent<TravelPurpose>(citizen, out TravelPurpose travelPurpose);
                bool useSCJ = m_UseSCJ.TryGetComponent(citizen, out SmallCityJobsComponent scjComponent) &&
                              scjComponent.m_UseSmallCityBehaviour;                

                Entity targetEntity = hasLeisure ? leisure.m_TargetAgent : Entity.Null;

                if (m_PropertyRenters.TryGetComponent(targetEntity, out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }

                

                bool knownLeisureVisit =  (hasLeisure && targetEntity == selectedEntity) 
                    || (m_LeisureVisitBuilding.TryGetValue(citizen, out Entity leisureBuilding) && leisureBuilding == selectedEntity);


                if (knownLeisureVisit && m_CurrentBuildings.TryGetComponent( citizen, out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity) {
                    Entity providerEntity = hasLeisure ? leisure.m_TargetAgent : Entity.Null;
                    customerInfo.Resource = GetResourceAmount(providerEntity);
                    customerInfo.NoService = HasNoService(providerEntity);
                    bool goingHome = false;
                    if (EntityManager.TryGetBuffer<TripNeeded>(
                            citizen,
                            true,
                            out DynamicBuffer<TripNeeded> trips)) {

                        for (int i = 0; i < trips.Length; i++) {
                            if (trips[i].m_Purpose == Purpose.GoingHome) {
                                goingHome = true;
                                break;
                            }
                        }
                    }

                    if (goingHome) {
                        customerInfo.Status = LeisureStatus.GoingToHome;
                        m_GoingHomeUntil[citizen] = DateTime.UtcNow.AddSeconds(5);
                    } else if (hasLeisure) {
                        customerInfo.Status = LeisureStatus.Leisuring;
                    } else if (!hasTravelPurpose && m_LeisureVisitBuilding.TryGetValue(citizen, out  leisureBuilding) &&
                        leisureBuilding == selectedEntity) {
                        customerInfo.Status = LeisureStatus.Idling;
                    } else {
                        continue;
                    }

                } else if (m_GoingHomeUntil.TryGetValue(citizen, out DateTime until) &&
                           DateTime.UtcNow < until) {

                    customerInfo.Status = LeisureStatus.GoingToHome;
                } else if (hasTravelPurpose &&
                           travelPurpose.m_Purpose == Purpose.Leisure &&
                           targetEntity == selectedEntity) {

                    customerInfo.Status = LeisureStatus.GoingToLeisure;
                    m_LeisureVisitBuilding[citizen] = targetEntity;
                } else {
                    m_GoingHomeUntil.Remove(citizen);
                    if (m_LeisureVisitBuilding.TryGetValue(citizen, out leisureBuilding) &&
                        m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding cb) &&
                        cb.m_CurrentBuilding != leisureBuilding &&
                        !(hasTravelPurpose &&
                        travelPurpose.m_Purpose == Purpose.Leisure)) {

                        m_LeisureVisitBuilding.Remove(citizen);
                    }
                    continue;
                }

                HouseholdMember householdMember =  EntityManager.GetComponentData<HouseholdMember>(citizen);

                customerInfo.Wealth = CitizenUIUtils.GetHouseholdWealth( EntityManager, householdMember.m_Household,
                    m_HappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>());
                customerInfo.Age = CitizenUIUtils.GetAge(EntityManager, citizen);
                if (useSCJ && hasLeisure && EntityManager.TryGetComponent<LeisureStartComponent>(
                    citizen, out LeisureStartComponent leisureStart)) {

                    Citizen citizenData = EntityManager.GetComponentData<Citizen>(citizen);

                    uint startFrame = leisureStart.m_StartFrame;
                    uint day = startFrame / 262144u;

                    float percent = Unity.Mathematics.Random.CreateFromIndex(
                        (uint)citizenData.m_PseudoRandom + 30000u + day
                    ).NextFloat(
                        Mod.Settings.LeisureIntervalMin / 100f,
                        Mod.Settings.LeisureIntervalMax / 100f
                    );

                    uint duration = leisure.m_LastPossibleFrame - startFrame;
                    uint endFrame = startFrame + (uint)(duration * percent);

                    long remainingFrames = Math.Max(0, (long)endFrame - currentFrame);

                    if (remainingFrames > 0) {
                        float remainingHours = remainingFrames * 24f / 262144f;

                        int hours = (int)remainingHours;
                        int minutes = (int)((remainingHours - hours) * 60f);

                        customerInfo.LeisureHours = $"{hours}:{minutes:00}";
                    } else {
                        customerInfo.LeisureHours = "";
                    }
                }

                if (useSCJ) {
                    Citizen citizenData =
                        EntityManager.GetComponentData<Citizen>(citizen);

                    customerInfo.LeisureDesire = scj.GetLeisureDesireLevel(citizenData);
                } else if (m_Desire.TryGetComponent(
                               citizen,
                               out DesiredLeisureTypeComponent desireComponent)) {

                    customerInfo.LeisureDesire = desireComponent.m_Desire;
                }

                customers.Add(customerInfo);
            }


            customers.Sort((a, b) => a.Customer.Index.CompareTo(b.Customer.Index));
            foreach (CustomerInfo customer in customers) {
                m_Customers.Add(customer);
            }
        }

        private int GetResourceAmount(Entity providerEntity) {
            if (providerEntity == Entity.Null ||
                !m_PrefabRefs.HasComponent(providerEntity))
                return -1;

            Entity prefab = m_PrefabRefs[providerEntity].m_Prefab;

            if (!m_IndustrialProcesses.HasComponent(prefab))
                return -1;

            Resource resource = m_IndustrialProcesses[prefab].m_Output.m_Resource;

            if (resource == Resource.NoResource ||
                !m_Resources.HasBuffer(providerEntity))
                return -1;

            return EconomyUtils.GetResources(
                resource,
                m_Resources[providerEntity]);
        }

        private bool HasNoService(Entity providerEntity) {
            return providerEntity != Entity.Null &&
                   m_ServiceAvailables.HasComponent(providerEntity) &&
                   m_ServiceAvailables[providerEntity].m_ServiceAvailable <= 0;
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

                writer.PropertyName("leisureHours");
                writer.Write(customer.LeisureHours ?? "");

                writer.PropertyName("status");
                writer.Write((int)customer.Status);

                writer.PropertyName("desire");
                writer.Write((int)customer.LeisureDesire);

                writer.PropertyName("age");
                writer.Write((int)customer.Age);

                writer.PropertyName("wealth");
                writer.Write((int)customer.Wealth);

                writer.PropertyName("resource");
                writer.Write(customer.Resource);

                writer.PropertyName("service");
                writer.Write(!customer.NoService);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
