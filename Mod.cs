using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Unity.Entities;

namespace BitulaMod
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(BitulaMod)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;
        public static Setting Settings { get; private set; }
        public static TriggerPrefab StartedLookingForWorkPrefab { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            Settings = m_Setting;
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
           

            PrefabSystem prefabSystem =
    World.DefaultGameObjectInjectionWorld
        .GetOrCreateSystemManaged<PrefabSystem>();

            StartedLookingForWorkPrefab = new TriggerPrefab
            {
                name = "BitulaMod_StartedLookingForWork"
            };

            Game.Prefabs.LifePathEvent lifePathComponent =
                StartedLookingForWorkPrefab
                    .AddOrGetComponent<Game.Prefabs.LifePathEvent>();

            lifePathComponent.m_EventType = (LifePathEventType)21;

            lifePathComponent.m_IsChirp = true;

            RandomLocalization randomLocalization =
                StartedLookingForWorkPrefab
                    .AddOrGetComponent<RandomLocalization>();

            randomLocalization.m_LocalizationID =
                "BitulaMod.LIFEPATH_STARTED_LOOKING_FOR_WORK";

            bool added =
                prefabSystem.AddPrefab(StartedLookingForWorkPrefab);

            log.Info($"Custom LifePath prefab added: {added}");


            AssetDatabase.global.LoadSettings(nameof(BitulaMod), m_Setting, new Setting(this));
            updateSystem.UpdateAt<WorkShiftUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<LifePathEventSenderSystem>(SystemUpdatePhase.UIUpdate);

          

            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.CitizenFindJobSystem>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.FindJobSystem>().Enabled = false;
            World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<Game.Simulation.ApplyToSchoolSystem>().Enabled = false;

            updateSystem.UpdateAt<CitizenFindJobSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<FindJobSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<ApplyToSchoolSystem>(SystemUpdatePhase.GameSimulation);


        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
