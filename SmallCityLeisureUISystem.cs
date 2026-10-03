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

namespace BitulaMod {
    enum LeisureStatus {
        None = 0,
        Leisuring = 1,
        GoingToLeisure = 2,
        Idling = 3,
        GoingToHome = 4,
        GoingToAnotherLeisure = 5,
        GoingToWork = 6,
        GoingToShop = 7
    }
    enum VisualDelay {
        None = 0,
        GoingHome = 1,
        NoService = 2,
        NoResource = 4,
        GoingToWork = 8,
        GoingToAnotherLeisure = 16,
        GoingToShop = 32,
    }


    public partial class SmallCityLeisureUISystem : UISystemBase {
        private class Delay {
            public DateTime m_Time;
            public VisualDelay m_Target;
        }
        private class CustomerInfo {
            public CustomerInfo(Entity citizen) {
                m_EndTime = DateTime.UtcNow.AddSeconds(5);
                Customer = citizen;
                LeisureHours = "";
                Resource = -1;
                delay = new Delay();
            }
            public Delay delay;
            public Entity Customer;
            public LeisureStatus Status;
            public byte LeisureDesire;
            public CitizenAgeKey Age;
            public HouseholdWealthKey Wealth;
            public int Resource;
            public bool NoService;
            public string LeisureHours;
            public int Money;
            public DateTime m_EndTime;

            public void RemoveDelay() {
                delay = null;
            }

            public void RemoveStatusDelay() {
                if (delay == null)
                    return;

                delay.m_Target &=
                    ~(VisualDelay.GoingHome |
                        VisualDelay.GoingToWork |
                        VisualDelay.GoingToShop |
                        VisualDelay.GoingToAnotherLeisure);

                if (delay.m_Target == 0) {
                    delay = null;
                }                 
            }
        }

        private readonly Dictionary<Entity, CustomerInfo> m_Customers = new();

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
        private ComponentLookup<ResourceBuyer> m_Shopping;
        private BufferLookup<Resources> m_Resources;
        private EntityQuery m_GoingToLeisureQuery;
        private readonly Dictionary<Entity, Entity> m_LeisureVisitBuilding = new();
        private Entity m_LastSelectedEntity = Entity.Null;

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
            m_Shopping = GetComponentLookup<ResourceBuyer>(true);


            AddBinding(m_CustomersBinding);
            RequireForUpdate(m_HappinessParameterQuery);
            Mod.log.Info("SmallCityLeisureUISystem created successfully");
        }

        protected override void OnUpdate() {
            base.OnUpdate();
            ref SystemState state = ref CheckedStateRef;


            


            Entity selectedEntity = m_SelectedInfoUISystem.selectedEntity;

            

            if (selectedEntity == Entity.Null ||
                !EntityManager.Exists(selectedEntity)) {
                m_CustomersBinding.Update();
                return;
            }

            m_PropertyRenters.Update(ref state);
            if (selectedEntity != m_LastSelectedEntity) {
                m_Customers.Clear();
                m_LeisureVisitBuilding.Clear();
                m_LastSelectedEntity = selectedEntity;
            }


            m_CurrentBuildings.Update(ref state);
            m_Targets.Update(ref state);            
            m_UseSCJ.Update(ref state);
            m_Desire.Update(ref state);
            m_PrefabRefs.Update(this);
            m_IndustrialProcesses.Update(this);
            m_Resources.Update(this);
            m_Shopping.Update(this);
            m_ServiceAvailables.Update(this);
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

        private void ClearCustomers(Entity selectedEntity) {
            DateTime now = DateTime.UtcNow;

            m_Customers
                .Where(x =>
                    !isValid(x.Key, selectedEntity) &&
                    (x.Value.delay == null || now >= x.Value.delay.m_Time) &&
                    now >= x.Value.m_EndTime)
                .Select(x => x.Key)
                .ToList()
                .ForEach(x => m_Customers.Remove(x));
        }


        private void AddCustomers(Entity selectedEntity, SmallCityJobs scj) {
            List<CustomerInfo> customers = new();
            using NativeArray<Entity> citizens = m_LeisureCitizenQuery.ToEntityArray(Allocator.Temp);
            SimulationSystem simulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            uint currentFrame = simulationSystem.frameIndex;
            foreach (Entity citizen in citizens) {
                if (isValid(citizen, selectedEntity) && !m_Customers.TryGetValue(citizen, out CustomerInfo customer)) {
                    customer = new CustomerInfo(citizen);
                    m_Customers.Add(citizen, customer);
                }
            }
            ClearCustomers(selectedEntity);
            foreach (CustomerInfo customer in m_Customers.Values) {
                Entity citizen = customer.Customer;                
                bool hasLeisure = EntityManager.TryGetComponent<Leisure>(citizen, out Leisure leisure);
                bool hasTravelPurpose = EntityManager.TryGetComponent<TravelPurpose>(citizen, out TravelPurpose travelPurpose);
                         

                Entity targetEntity = hasLeisure ? leisure.m_TargetAgent : Entity.Null;

                if (m_PropertyRenters.TryGetComponent(targetEntity, out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }                

                bool knownLeisureVisit =  (hasLeisure && targetEntity == selectedEntity) 
                    || (m_LeisureVisitBuilding.TryGetValue(citizen, out Entity leisureBuilding) && leisureBuilding == selectedEntity);

                bool isInSelectedBuilding = m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding currentBuilding) &&
                    currentBuilding.m_CurrentBuilding == selectedEntity;

                if (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Leisure && targetEntity == selectedEntity) {
                    customer.Status = LeisureStatus.GoingToLeisure;
                    m_LeisureVisitBuilding[citizen] = targetEntity;
                    customer.RemoveDelay();
                } else if (IsDelayActive(citizen, selectedEntity, customer) ) {
                    //Handled by IsDelay
                } else if ((knownLeisureVisit || hasLeisure) && isInSelectedBuilding) {
                    customer.RemoveStatusDelay();
                    if (hasLeisure) {
                        customer.Status = LeisureStatus.Leisuring;
                    } else if (!hasTravelPurpose && m_LeisureVisitBuilding.TryGetValue(citizen, out leisureBuilding) &&
                        leisureBuilding == selectedEntity) {
                        customer.Status = LeisureStatus.Idling;
                    } else {
                        continue;
                    }

                } else {
                    customer.RemoveDelay();
                    continue;
                }

                FillUIData(customer, currentFrame, ref scj);

                customers.Add(customer);
            }


            customers.Sort((a, b) => a.Customer.Index.CompareTo(b.Customer.Index));
            foreach (CustomerInfo customer in customers) {
                m_Customers[customer.Customer] = customer;
            }
        }

        private bool isValid(Entity citizen, Entity selectedEntity) {
            bool hasLeisure = EntityManager.TryGetComponent<Leisure>(citizen, out Leisure leisure);
            bool hasTravelPurpose = EntityManager.TryGetComponent<TravelPurpose>(citizen, out TravelPurpose travelPurpose);


            Entity targetEntity = hasLeisure ? leisure.m_TargetAgent : Entity.Null;

            if (!hasLeisure && m_Targets.TryGetComponent(citizen, out Target target)) {
                targetEntity = target.m_Target;
            }

            if (m_PropertyRenters.TryGetComponent(targetEntity, out PropertyRenter renter)) {
                targetEntity = renter.m_Property;
            }

            bool knownLeisureVisit = (hasLeisure && targetEntity == selectedEntity)
                || (m_LeisureVisitBuilding.TryGetValue(citizen, out Entity leisureBuilding) && leisureBuilding == selectedEntity);

            bool isInSelectedBuilding = m_CurrentBuildings.TryGetComponent(citizen, out CurrentBuilding currentBuilding) &&
                currentBuilding.m_CurrentBuilding == selectedEntity;

            return (hasTravelPurpose && travelPurpose.m_Purpose == Purpose.Leisure && targetEntity == selectedEntity) || (knownLeisureVisit && isInSelectedBuilding);
        }

        private void FillUIData(CustomerInfo customer, uint currentFrame, ref SmallCityJobs scj) {
            Entity citizen = customer.Customer;
            HouseholdMember householdMember = EntityManager.GetComponentData<HouseholdMember>(citizen);
            bool hasLeisure = EntityManager.TryGetComponent<Leisure>(citizen, out Leisure leisure);
            customer.Wealth = CitizenUIUtils.GetHouseholdWealth(EntityManager, householdMember.m_Household,
                m_HappinessParameterQuery.GetSingleton<CitizenHappinessParameterData>());
            customer.Age = CitizenUIUtils.GetAge(EntityManager, citizen);
            customer.Money = GetAvailableMoney(householdMember.m_Household);
            bool useSCJ = m_UseSCJ.TryGetComponent(citizen, out SmallCityJobsComponent scjComponent) &&
                              scjComponent.m_UseSmallCityBehaviour;
            Citizen citizenData = EntityManager.GetComponentData<Citizen>(citizen);
            if (useSCJ && hasLeisure && EntityManager.TryGetComponent<LeisureStartComponent>(
                citizen, out LeisureStartComponent leisureStart)) {                

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

                    customer.LeisureHours = $"{hours:00}:{minutes:00}";
                } else {
                    customer.LeisureHours = "";
                }
            }

            if (useSCJ) {

                customer.LeisureDesire = scj.GetLeisureDesireLevel(citizenData);
            } else if (m_Desire.TryGetComponent(
                           citizen,
                           out DesiredLeisureTypeComponent desireComponent)) {

                customer.LeisureDesire = desireComponent.m_Desire;
            }
        }
        

        private bool IsDelayActive( Entity citizen, Entity selectedEntity, CustomerInfo customer) {
            customer.Resource = -1;
            customer.NoService = false;

            bool hasLeisure = EntityManager.TryGetComponent<Leisure>(
                citizen,
                out Leisure leisure
            );

            if (hasLeisure) {
                Entity targetEntity = leisure.m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(
                        targetEntity,
                        out PropertyRenter renter)) {
                    targetEntity = renter.m_Property;
                }

                if (targetEntity == selectedEntity) {
                    Entity providerEntity = leisure.m_TargetAgent;

                    if (HasNoResource(providerEntity)) {
                        customer.Resource = 0;
                        SetVisualDelay(customer, VisualDelay.NoResource);
                    }

                    if (HasNoService(providerEntity)) {
                        customer.NoService = true;
                        SetVisualDelay(customer, VisualDelay.NoService);
                    }
                }
            }

            bool liveStatus = false;
            bool isInSelectedBuilding =  (m_LeisureVisitBuilding.TryGetValue(citizen, out Entity leisureBuilding) &&
             leisureBuilding == selectedEntity);

            if (HasTripPurpose(citizen, Purpose.GoingHome)) {
                customer.Status = LeisureStatus.GoingToHome;
                SetVisualDelay(customer, VisualDelay.GoingHome);
                liveStatus = true;
            } else if (HasTripPurpose(citizen, Purpose.GoingToWork)) {
                customer.Status = LeisureStatus.GoingToWork;
                SetVisualDelay(customer, VisualDelay.GoingToWork);
                liveStatus = true;
            } else if (HasTripPurpose(citizen, Purpose.Shopping)) {
                customer.Status = LeisureStatus.GoingToShop;
                SetVisualDelay(customer, VisualDelay.GoingToShop);
                liveStatus = true;
            } else if (GoingToOtherLeisure(citizen, selectedEntity)) {
                customer.Status = LeisureStatus.GoingToAnotherLeisure;
                SetVisualDelay(customer, VisualDelay.GoingToAnotherLeisure);
                liveStatus = true;
            }

            if (customer.delay == null || DateTime.UtcNow >= customer.delay.m_Time) {
                customer.RemoveDelay();
                return liveStatus;
            }

            if ((customer.delay.m_Target & VisualDelay.NoService) != 0) {
                customer.NoService = true;
            }

            if ((customer.delay.m_Target & VisualDelay.NoResource) != 0) {
                customer.Resource = 0;
            }

            // Current live status has priority over an older delayed status.
            if (liveStatus) {
                return true;
            }

            if ((customer.delay.m_Target & VisualDelay.GoingHome) != 0) {
                customer.Status = LeisureStatus.GoingToHome;
                return true;
            }

            if ((customer.delay.m_Target & VisualDelay.GoingToWork) != 0) {
                customer.Status = LeisureStatus.GoingToWork;
                return true;
            }

            if ((customer.delay.m_Target & VisualDelay.GoingToShop) != 0) {
                customer.Status = LeisureStatus.GoingToShop;
                return true;
            }

            if ((customer.delay.m_Target & VisualDelay.GoingToAnotherLeisure) != 0) {
                customer.Status = LeisureStatus.GoingToAnotherLeisure;
                return true;
            }

            return false;
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

        private bool HasNoResource(Entity providerEntity) {
            if (providerEntity == Entity.Null ||
                !m_PrefabRefs.HasComponent(providerEntity))
                return false;

            Entity prefab = m_PrefabRefs[providerEntity].m_Prefab;

            if (!m_IndustrialProcesses.HasComponent(prefab))
                return false;

            Resource resource = m_IndustrialProcesses[prefab].m_Output.m_Resource;

            return resource != Resource.NoResource &&
                   m_Resources.HasBuffer(providerEntity) &&
                   EconomyUtils.GetResources(resource, m_Resources[providerEntity]) <= 0;
        }

        private bool HasNoService(Entity providerEntity) {
            return providerEntity != Entity.Null &&
                   m_ServiceAvailables.HasComponent(providerEntity) &&
                   m_ServiceAvailables[providerEntity].m_ServiceAvailable <= 0;
        }

        private int GetAvailableMoney(Entity household) {
            if (household == Entity.Null ||
                !m_Resources.HasBuffer(household))
                return 0;

            return EconomyUtils.GetResources(
                Resource.Money,
                m_Resources[household]);
        }

        private void SetVisualDelay(CustomerInfo customer, VisualDelay target) {
            DateTime now = DateTime.UtcNow;

            if (customer.delay == null || now >= customer.delay.m_Time) {
                customer.delay = new Delay {
                    m_Time = now.AddSeconds(5),
                    m_Target = target
                };
                return;
            }

            if ((customer.delay.m_Target & target) != 0)
                return;

            customer.delay.m_Target |= target;
            customer.delay.m_Time = now.AddSeconds(5);
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

        private bool GoingToOtherLeisure(Entity citizen, Entity selectedEntity) {
            if (!EntityManager.TryGetBuffer<TripNeeded>(
                    citizen,
                    true,
                    out DynamicBuffer<TripNeeded> trips)) {
                return false;
            }

            for (int i = 0; i < trips.Length; i++) {
                if (trips[i].m_Purpose != Purpose.Leisure)
                    continue;

                Entity target = trips[i].m_TargetAgent;

                if (m_PropertyRenters.TryGetComponent(target, out PropertyRenter renter))
                    target = renter.m_Property;

                if (target != selectedEntity)
                    return true;
            }

            return false;
        }



        private void BindCustomers(IJsonWriter writer) {
            writer.ArrayBegin((uint)m_Customers.Count);

            foreach (CustomerInfo customer in m_Customers.Values) {
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

                writer.PropertyName("money");
                writer.Write(customer.Money);

                writer.PropertyName("service");
                writer.Write(!customer.NoService);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
