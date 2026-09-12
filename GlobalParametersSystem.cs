using Colossal.Serialization.Entities;
using Game;
using Game.Prefabs;
using System;
using Unity.Entities;

namespace BitulaMod {
    
    public partial class GlobalParametersSystem : GameSystemBase {
        private EntityQuery m_EconomyParameterQuery;
        private bool m_PreviousFullTrafficSimulation;
        private float m_VanillaTrafficReduction;
        private bool m_VanillaTrafficReductionCaptured;

        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 256;
        }

        protected override void OnCreate() {
            base.OnCreate();

            m_EconomyParameterQuery = GetEntityQuery(
                ComponentType.ReadWrite<EconomyParameterData>());

            RequireForUpdate(m_EconomyParameterQuery);
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
            }

            Mod.log.Info($"Vanilla TrafficReduction = {m_VanillaTrafficReduction}");
            Mod.log.Info($"TrafficReduction = {data.m_TrafficReduction}");
        }

        protected override void OnUpdate() {
            EconomyParameterData data =
                m_EconomyParameterQuery.GetSingleton<EconomyParameterData>();

            bool fullTrafficSimulation = Mod.Settings.FullTrafficSimulation;

            if (!m_VanillaTrafficReductionCaptured) {
                m_VanillaTrafficReduction = data.m_TrafficReduction;
                m_VanillaTrafficReductionCaptured = true;
                m_PreviousFullTrafficSimulation = !fullTrafficSimulation;
            }

            if (fullTrafficSimulation != m_PreviousFullTrafficSimulation) {
                data.m_TrafficReduction =
                    fullTrafficSimulation ? 0f : m_VanillaTrafficReduction;

                m_EconomyParameterQuery.SetSingleton(data);
                m_PreviousFullTrafficSimulation = fullTrafficSimulation;
            }

            if (fullTrafficSimulation && data.m_TrafficReduction != 0f) {
                throw new InvalidOperationException(
                    $"TrafficReduction expected to be 0 but was {data.m_TrafficReduction}");
            }
        }
    }
    
}
