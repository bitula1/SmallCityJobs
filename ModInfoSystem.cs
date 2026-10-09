using Colossal.Serialization.Entities;
using Game;
using UnityEngine;

namespace BitulaMod {
    public partial class ModInfoSystem : GameSystemBase {
        private const string ChangelogVersionKey = "BitulaMod.LastShownChangelogVersion";
        private string m_VersionToShow;
        public override int GetUpdateInterval(SystemUpdatePhase phase) {
            return 16;
        }
        protected override void OnUpdate() {
            if (m_VersionToShow == null)
                return;

            string version = m_VersionToShow;
            m_VersionToShow = null;

            ModInfo.ShowChangelog();

            PlayerPrefs.SetString(ChangelogVersionKey, version);
            PlayerPrefs.Save();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            base.OnGameLoadingComplete(purpose, mode);
            string version = ModInfo.GetVersion();

            m_VersionToShow =
                mode == GameMode.Game &&
                !string.IsNullOrWhiteSpace(version) &&
                PlayerPrefs.GetString(ChangelogVersionKey, "") != version
                    ? version : null;

        }


    }
}
