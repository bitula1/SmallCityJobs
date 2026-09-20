using Colossal.UI.Binding;
using Game;
using Game.Citizens;
using Game.City;
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
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BitulaMod
{
    public partial class WorkShiftUISystem : UISystemBase
    {
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 64;
        }

        private SelectedInfoUISystem m_SelectedInfoUISystem;
        private ValueBinding<string> m_WorkHoursBinding;
        private ValueBinding<uint> m_LastDayResourceCostBinding;
        private ValueBinding<uint> m_RemainingDaysOff;
        private ValueBinding<bool> m_DaysOff;
        private ValueBinding<bool> m_IsHouseholdSelectedBinding;
        private EntityQuery m_EconomyParameterQuery;
        private SimulationSystem m_SimulationSystem;
        private CitySystem m_CitySystem;
        private EntityQuery m_TimeQuery;
        private Game.Common.TimeData m_TimeData;
        private SmallCityJobs m_SmallCityJobs;


        protected override void OnCreate()
        {
            base.OnCreate();

            m_SelectedInfoUISystem =
                World.GetOrCreateSystemManaged<SelectedInfoUISystem>();

            m_EconomyParameterQuery = GetEntityQuery(
                ComponentType.ReadOnly<EconomyParameterData>()
            );

            m_SimulationSystem =  World.GetOrCreateSystemManaged<SimulationSystem>();

            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();

            m_TimeQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Common.TimeData>());

            m_WorkHoursBinding = new ValueBinding<string>("BitulaMod", "workHours", "");

            AddBinding(m_WorkHoursBinding);

            m_LastDayResourceCostBinding = new ValueBinding<uint>("BitulaMod", "lastDayResourceCost", 0);

            AddBinding(m_LastDayResourceCostBinding);

            m_IsHouseholdSelectedBinding = new ValueBinding<bool>("BitulaMod", "isHouseholdSelected", false);

            AddBinding(m_IsHouseholdSelectedBinding);

            m_DaysOff = new ValueBinding<bool>("BitulaMod", "isDaysOff", false);

            AddBinding(m_DaysOff);

            m_RemainingDaysOff = new ValueBinding<uint>("BitulaMod", "remainingDaysOff", 0);

            AddBinding(m_RemainingDaysOff);


            RequireForUpdate(m_TimeQuery);
            Mod.log.Info("WorkShiftUISystem created successfully");

        }

        protected override void OnUpdate()
        {
            m_SmallCityJobs = SmallCityJobs.Create();
            updatePrevResourceCost();
            updateWorkHours();
            UpdateDaysOff();

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
        public int GetRemainingOffDays(Citizen citizen, ref EconomyParameterData economyParameters, uint frame,
            Game.Common.TimeData timeData, int population) {

            int vanillaThreshold = math.min(
                40,
                Mathf.RoundToInt(
                    100f / math.max(
                        1f,
                        math.sqrt(economyParameters.m_TrafficReduction * (float)population))));

            int threshold = vanillaThreshold;

            if (Mod.Settings.ReducedDaysOff) {
                int passedMilestones = math.max(0, population - 1) / Mod.Settings.JobSeekerMilestone;
                int appliedPercentage = math.min(100, passedMilestones * Mod.Settings.JobSeekerFailureIncrement);

                const int smallCityThreshold = 79;

                threshold = Mathf.RoundToInt(math.lerp(
                    smallCityThreshold,
                    vanillaThreshold,
                    appliedPercentage / 100f));
            }

            int currentDay = TimeSystem.GetDay(frame, timeData);
            int offDays = 0;

            for (int day = currentDay; ; day++) {
                bool isOffDay =
                    Unity.Mathematics.Random.CreateFromIndex(
                        (uint)((int)citizen.m_PseudoRandom + day))
                    .NextInt(100) > threshold;

                if (!isOffDay)
                    break;

                offDays++;
            }

            return offDays;
        }




        private void UpdateDaysOff() {
            Entity citizenEntity =  m_SelectedInfoUISystem.selectedEntity;
            if (citizenEntity == Entity.Null ||
                !EntityManager.Exists(citizenEntity) ||
                !EntityManager.HasComponent<Citizen>(citizenEntity) ||
                !EntityManager.HasComponent<Worker>(citizenEntity)) {

                m_DaysOff.Update(false);
                m_RemainingDaysOff.Update(0);
                return;
            }
            m_TimeData = m_TimeQuery.GetSingleton<Game.Common.TimeData>();


            Citizen citizen =
                EntityManager.GetComponentData<Citizen>(citizenEntity);

            EconomyParameterData economyParameters =
                m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();

            Population populationData =
                EntityManager.GetComponentData<Population>(m_CitySystem.City);

            int population = populationData.m_Population;
            uint frame = m_SimulationSystem.frameIndex;

            bool isDaysOff = m_SmallCityJobs.IsTodayOffDay(
                citizen,
                ref economyParameters,
                frame,
                m_TimeData,
                population);

            m_DaysOff.Update(isDaysOff);

            if (isDaysOff) {
                int remainingOffDays = GetRemainingOffDays(
                    citizen,
                    ref economyParameters,
                    frame,
                    m_TimeData,
                    population);

                m_RemainingDaysOff.Update(
                    (uint)math.max(0, remainingOffDays - 1));
            } else {
                m_RemainingDaysOff.Update(0);
            }
        }

        public bool IsTodayOffDay(Entity citizenEntity) {
            m_TimeData = m_TimeQuery.GetSingleton<Game.Common.TimeData>();


            Citizen citizen =
                EntityManager.GetComponentData<Citizen>(citizenEntity);

            EconomyParameterData economyParameters =
                m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();

            Population populationData =
                EntityManager.GetComponentData<Population>(m_CitySystem.City);

            int population = populationData.m_Population;
            uint frame = m_SimulationSystem.frameIndex;

            return m_SmallCityJobs.IsTodayOffDay(
                citizen,
                ref economyParameters,
                frame,
                m_TimeData,
                population);
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