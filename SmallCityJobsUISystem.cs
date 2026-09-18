using Colossal.UI.Binding;
using Game;
using Game.Buildings;
using Game.Companies;
using Game.UI;
using Game.UI.InGame;
using System.Collections.Generic;
using Unity.Entities;

namespace BitulaMod {
    public partial class SmallCityJobsUISystem : UISystemBase {
        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private RawValueBinding m_WorkersBinding;
        private readonly List<Entity> m_Workers = new();
        private BufferLookup<Renter> m_Renters;
        private BufferLookup<Employee> m_Employees;
        private NameSystem m_NameSystem;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 64;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_Renters = GetBufferLookup<Renter>(true);
            m_Employees = GetBufferLookup<Employee>(true);
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();

            m_SelectedInfoUISystem =
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>();

            m_WorkersBinding = new RawValueBinding( "BitulaMod", "workers", BindWorkers);

            AddBinding(m_WorkersBinding);

            Mod.log.Info("SmallCityJobsUISystem created successfully");
        }

        protected override void OnUpdate() {
            base.OnUpdate();

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
                Mod.log.Info($"Selected building has {renters.Length} renters");
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

                if (worker != Entity.Null &&
                    EntityManager.Exists(worker)) {
                    m_Workers.Add(worker);
                }
            }
        }

        private void BindWorkers(IJsonWriter writer) {
            writer.ArrayBegin((uint)m_Workers.Count);

            foreach (Entity worker in m_Workers) {
                writer.TypeBegin("Worker");

                writer.PropertyName("entity");
                writer.TypeBegin("Entity");

                writer.PropertyName("index");
                writer.Write(worker.Index);

                writer.PropertyName("version");
                writer.Write(worker.Version);

                writer.TypeEnd();

                writer.PropertyName("name");
                m_NameSystem.BindName(writer, worker);

                writer.TypeEnd();
            }

            writer.ArrayEnd();
        }
    }
}
