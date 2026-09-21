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
using Unity.Entities;

namespace BitulaMod {
    enum WorkerStatus {
        None = 0,
        Working = 1,
        NotWorking = 2,
        DayOff = 3,
        GoingToWork = 4,
        EmployerGone = 5,
        WorkplaceGone = 6,
        WorkingElsewhere = 7,
    }
    public partial class SmallCityJobsUISystem : UISystemBase {
        private struct WorkerInfo {
            public Entity Worker;
            public WorkerStatus Status;
            public CitizenJobLevelKey JobLevel;
            public CitizenEducationLevel EducationLevel;
        }
        private readonly List<WorkerInfo> m_Workers = new();
        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private RawValueBinding m_WorkersBinding;
        
        private BufferLookup<Renter> m_Renters;
        private BufferLookup<Employee> m_Employees;
        private NameSystem m_NameSystem;
        private SimulationSystem m_SimulationSystem;
        private WorkShiftUISystem m_WorkShiftUISystem;
        private SmallCityJobs m_SmallCityJobs;
        private EntityQuery m_TimeQuery;
        private TimeData m_TimeData;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 64;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Renters = GetBufferLookup<Renter>(true);
            m_Employees = GetBufferLookup<Employee>(true);
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();
            
            m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
            m_SelectedInfoUISystem = World.GetOrCreateSystemManaged<SelectedInfoUISystem>();
            m_WorkShiftUISystem = World.GetOrCreateSystemManaged<WorkShiftUISystem>();

            m_WorkersBinding = new RawValueBinding( "BitulaMod", "workers", BindWorkers);
            m_TimeQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Common.TimeData>());

            AddBinding(m_WorkersBinding);
            RequireForUpdate(m_TimeQuery);

            Mod.log.Info("SmallCityJobsUISystem created successfully");
        }

        protected override void OnUpdate() {
            base.OnUpdate();

            ref SystemState state = ref CheckedStateRef;
            m_TimeData = m_TimeQuery.GetSingleton<TimeData>();

            m_Renters.Update(this);
            m_Employees.Update(this);

            m_Workers.Clear();

            Entity selectedEntity = m_SelectedInfoUISystem.selectedEntity;

            if (selectedEntity == Entity.Null ||
                !EntityManager.Exists(selectedEntity)) {
                m_WorkersBinding.Update();
                return;
            }

            // selected entity itself might have employees
            AddWorkers(selectedEntity);

            // for a building, employees are usually on its renter/company
            if (m_Renters.TryGetBuffer(
                    selectedEntity,
                    out DynamicBuffer<Renter> renters)) {
                for (int i = 0; i < renters.Length; i++) {
                    Entity renter = renters[i].m_Renter;

                    if (renter != Entity.Null &&
                        EntityManager.Exists(renter)) {
                        AddWorkers(renter);
                    }
                }
            }

            m_WorkersBinding.Update();
        }

        private void AddWorkers(Entity workplace) {
            if (!m_Employees.TryGetBuffer(
                    workplace,
                    out DynamicBuffer<Employee> employees)) {                
                return;
            }

            for (int i = 0; i < employees.Length; i++) {
                Entity worker = employees[i].m_Worker;


                if (worker != Entity.Null && EntityManager.Exists(worker) &&  EntityManager.TryGetComponent<Worker>(worker, out Worker workerData)
                    && EntityManager.TryGetComponent<Citizen>(worker, out Citizen citizenData)) {
                    m_Workers.Add(new WorkerInfo {
                        Worker = worker,
                        Status = GetWorkerStatus(worker),
                        JobLevel = (CitizenJobLevelKey)workerData.m_Level,
                        EducationLevel = (CitizenEducationLevel)citizenData.GetEducationLevel()
                    });
                }
            }
        }

        private WorkerStatus GetWorkerStatus(Entity worker) {
            Worker workerData = EntityManager.GetComponentData<Worker>(worker);
            Entity workplace = workerData.m_Workplace;

            if (workplace == Entity.Null || !EntityManager.Exists(workplace)) {
                return WorkerStatus.WorkplaceGone;
            }

            Entity workplaceBuilding = workplace;

            if (EntityManager.HasComponent<CompanyData>(workplace)) {
                if (!EntityManager.HasComponent<PropertyRenter>(workplace)) {
                    return WorkerStatus.EmployerGone;
                }

                PropertyRenter renter =
                    EntityManager.GetComponentData<PropertyRenter>(workplace);

                if (renter.m_Property == Entity.Null || !EntityManager.Exists(renter.m_Property)) {
                    return WorkerStatus.EmployerGone;
                }

                workplaceBuilding = renter.m_Property;
            } else if (EntityManager.HasComponent<PropertyRenter>(workplace)) {
                PropertyRenter renter = EntityManager.GetComponentData<PropertyRenter>(workplace);

                if (renter.m_Property == Entity.Null || !EntityManager.Exists(renter.m_Property)) {
                    return WorkerStatus.EmployerGone;
                }

                workplaceBuilding = renter.m_Property;
            } else if (!EntityManager.HasComponent<Building>(workplace)) {
                return WorkerStatus.EmployerGone;
            }

            if (EntityManager.HasComponent<CurrentBuilding>(worker)) {
                CurrentBuilding currentBuilding =
                    EntityManager.GetComponentData<CurrentBuilding>(worker);

                if (currentBuilding.m_CurrentBuilding == workplaceBuilding) {
                    return WorkerStatus.Working;
                } else if (EntityManager.HasComponent<TravelPurpose>(worker)) {
                    TravelPurpose travelPurpose =
                        EntityManager.GetComponentData<TravelPurpose>(worker);

                    if (travelPurpose.m_Purpose == Purpose.Working || travelPurpose.m_Purpose == Purpose.GoingToWork) {
                        return WorkerStatus.WorkingElsewhere;
                    }
                }
            }

            if (EntityManager.HasComponent<TravelPurpose>(worker)) {
                TravelPurpose travelPurpose =
                    EntityManager.GetComponentData<TravelPurpose>(worker);

                if (travelPurpose.m_Purpose == Purpose.GoingToWork) {
                    return WorkerStatus.GoingToWork;
                }
            }

            if (EntityManager.HasComponent<Citizen>(worker) && m_WorkShiftUISystem.IsTodayOffDay(worker)) {
                return WorkerStatus.DayOff;
            }

            return WorkerStatus.NotWorking;
        }

        private void BindWorkers(IJsonWriter writer) {
            writer.ArrayBegin((uint)m_Workers.Count);

            foreach (WorkerInfo worker in m_Workers) {
                writer.TypeBegin("Worker");

                writer.PropertyName("entity");
                writer.TypeBegin("Entity");

                writer.PropertyName("index");
                writer.Write(worker.Worker.Index);

                writer.PropertyName("version");
                writer.Write(worker.Worker.Version);

                writer.TypeEnd();

                writer.PropertyName("name");
                m_NameSystem.BindName(writer, worker.Worker);

                writer.PropertyName("status");
                writer.Write((int)worker.Status);

                writer.PropertyName("jobLevel");
                writer.Write((int)worker.JobLevel);

                writer.PropertyName("educationLevel");
                writer.Write((int)worker.EducationLevel);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
