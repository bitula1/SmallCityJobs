using Colossal.UI.Binding;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Triggers;
using Game.UI;
using Game.UI.InGame;
using Game.UI.Localization;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace BitulaMod
{
    public partial class WorkShiftUISystem : UISystemBase
    {
        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private ValueBinding<string> m_WorkHoursBinding;
        private ValueBinding<uint> m_LastDayResourceCostBinding;
        private ValueBinding<bool> m_IsHouseholdSelectedBinding;
        private EntityQuery m_EconomyParameterQuery;


        protected override void OnCreate()
        {
            base.OnCreate();

            m_SelectedInfoUISystem =
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>();

            m_EconomyParameterQuery = GetEntityQuery(
                ComponentType.ReadOnly<EconomyParameterData>()
            );

            m_WorkHoursBinding = new ValueBinding<string>("BitulaMod", "workHours", "");

            AddBinding(m_WorkHoursBinding);

            m_LastDayResourceCostBinding = new ValueBinding<uint>("BitulaMod", "lastDayResourceCost", 0);

            AddBinding(m_LastDayResourceCostBinding);

            m_IsHouseholdSelectedBinding = new ValueBinding<bool>("BitulaMod", "isHouseholdSelected", false);

            AddBinding(m_IsHouseholdSelectedBinding);

            Mod.log.Info("WorkShiftUISystem created successfully");

        }

        protected override void OnUpdate()
        {

            updatePrevResourceCost();
            updateWorkHours();
            //updateCitizenEvents();

        }

        private static string FormatTime(float normalizedTime)
        {
            int totalMinutes =
                (int)math.round(math.frac(normalizedTime) * 1440f);

            totalMinutes %= 1440;

            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            return $"{hour:00}:{minute:00}";
        }

        private void updatePrevResourceCost()
        {
            Entity selectedEntity =
                m_SelectedInfoUISystem.selectedEntity;

            if (selectedEntity == Entity.Null ||
                !EntityManager.Exists(selectedEntity))
            {
                m_WorkHoursBinding.Update("");
                m_LastDayResourceCostBinding.Update(0);
                return;
            }

            bool isHouseholdSelected =
                selectedEntity != Entity.Null &&
                EntityManager.Exists(selectedEntity) &&
                EntityManager.HasComponent<Game.Citizens.Household>(selectedEntity);

            m_IsHouseholdSelectedBinding.Update(isHouseholdSelected);

            Entity citizenEntity = selectedEntity;

            // A selected rendered human may refer to the actual citizen entity.
            if (EntityManager.HasComponent<Game.Creatures.Resident>(
                selectedEntity))
            {
                Game.Creatures.Resident resident =
                    EntityManager.GetComponentData<Game.Creatures.Resident>(
                        selectedEntity);

                citizenEntity = resident.m_Citizen;
            }

            // Update household resource cost.
            uint lastDayResourceCost = 0;
            Entity householdEntity = Entity.Null;

            // The household/family itself is selected.
            if (EntityManager.HasComponent<Household>(selectedEntity))
            {
                householdEntity = selectedEntity;
            }
            // An individual or rendered citizen is selected.
            else if (citizenEntity != Entity.Null &&
                     EntityManager.Exists(citizenEntity) &&
                     EntityManager.HasComponent<HouseholdMember>(
                         citizenEntity))
            {
                HouseholdMember member =
                    EntityManager.GetComponentData<HouseholdMember>(
                        citizenEntity);

                householdEntity = member.m_Household;
            }

            if (householdEntity != Entity.Null &&
                EntityManager.Exists(householdEntity) &&
                EntityManager.HasComponent<Household>(
                    householdEntity))
            {
                Household household =
                    EntityManager.GetComponentData<Household>(
                        householdEntity);

                lastDayResourceCost =
                    household.m_ShoppedValueLastDay;
            }

            m_LastDayResourceCostBinding.Update(
                lastDayResourceCost);
        }

        private void updateWorkHours()
        {
            Entity citizenEntity =
                m_SelectedInfoUISystem.selectedEntity;
            if (citizenEntity == Entity.Null ||
                !EntityManager.Exists(citizenEntity) ||
                !EntityManager.HasComponent<Citizen>(
                    citizenEntity) ||
                !EntityManager.HasComponent<Worker>(
                    citizenEntity) ||
                m_EconomyParameterQuery.IsEmptyIgnoreFilter)
            {
                m_WorkHoursBinding.Update("");
                return;
            }

            Citizen citizen =
                EntityManager.GetComponentData<Citizen>(
                    citizenEntity);

            Worker worker =
                EntityManager.GetComponentData<Worker>(
                    citizenEntity);

            EconomyParameterData economyParameters =
                m_EconomyParameterQuery
                    .GetSingleton<EconomyParameterData>();

            float2 workTime = WorkerSystem.GetTimeToWork(
                citizen,
                worker,
                ref economyParameters,
                true
            );

            string workHours =
                $"{FormatTime(workTime.x)}–{FormatTime(workTime.y)}";

            m_WorkHoursBinding.Update(workHours);
        }

        
        }
    }