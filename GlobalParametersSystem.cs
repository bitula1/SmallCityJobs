using Colossal.Serialization.Entities;
using Game;
using Game.City;
using Game.Companies;
using Game.Prefabs;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace BitulaMod {
    
    public partial class GlobalParametersSystem : GameSystemBase {
        private EntityQuery m_EconomyParameterQuery;
        private float m_VanillaTrafficReduction;
        private bool m_VanillaTrafficReductionCaptured;
        private EntityQuery m_PopulationQuery;


        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 256;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_EconomyParameterQuery = GetEntityQuery(ComponentType.ReadWrite<EconomyParameterData>());
            m_PopulationQuery = GetEntityQuery(ComponentType.ReadOnly<Population>());

            RequireForUpdate(m_EconomyParameterQuery);
            RequireForUpdate(m_PopulationQuery);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);

            if (!mode.IsGame()) {
                return;
            }

            EconomyParameterData data =
                m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();

            if (!m_VanillaTrafficReductionCaptured) {
                m_VanillaTrafficReduction = data.m_TrafficReduction;
                m_VanillaTrafficReductionCaptured = true;

                Mod.log.Info($"Captured Vanilla TrafficReduction = {m_VanillaTrafficReduction}");
            }

            Mod.log.Info($"Vanilla TrafficReduction = {m_VanillaTrafficReduction}");
            Mod.log.Info($"Saved TrafficReduction = {data.m_TrafficReduction}");
        }

        protected override void OnUpdate() {
            EconomyParameterData data =
                m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();

            bool fullTrafficSimulation = Mod.Settings.FullTrafficSimulation;
            bool progressiveTrafficSimulation = Mod.Settings.ProgressiveTrafficSimulation;

            float targetTrafficReduction = m_VanillaTrafficReduction;

            if (fullTrafficSimulation) {
                targetTrafficReduction = 0f;
            } else if (progressiveTrafficSimulation) {
                int population =
                    m_PopulationQuery.GetSingleton<Population>().m_Population;

                int appliedPercentage = SmallCityJobs.GetAppliedPercentage(
                    population,
                    Mod.Settings.JobSeekerMilestone,
                    Mod.Settings.JobSeekerFailureIncrement);

                targetTrafficReduction = math.lerp(
                    0f,
                    m_VanillaTrafficReduction,
                    appliedPercentage / 100f);
            }

            if (data.m_TrafficReduction != targetTrafficReduction) {
                Mod.log.Info($"TrafficReduction changed from {data.m_TrafficReduction:0.#####} to {targetTrafficReduction:0.#####}");

                data.m_TrafficReduction = targetTrafficReduction;
                m_EconomyParameterQuery.SetSingleton(data);
            }
        }
    }
    
}
