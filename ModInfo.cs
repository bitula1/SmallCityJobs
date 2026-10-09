using Game.SceneFlow;
using Game.UI.Localization;
using Game.UI;
using System.Xml.Linq;



namespace BitulaMod {
    public class ModInfo {        
        public static void ShowChangelog() {
            string changelog = GetChangelog();

            if (string.IsNullOrWhiteSpace(changelog))
                return;

            GameManager.instance.userInterface.appBindings.ShowMessageDialog(
                new MessageDialog(
                    $"Small City Jobs {GetVersion()} - Changelog",
                    changelog,
                    LocalizedString.Id("Common.ERROR_ACTION[Continue]")
                ),
                null
            );
        }

        public static string GetChangelog() {
            using (var stream = typeof(ModInfo).Assembly
                .GetManifestResourceStream("BitulaMod.PublishConfiguration.xml")) {

                if (stream == null)
                    return null;

                var element = XDocument.Load(stream).Root?.Element("ChangeLog");

                return ((string)element?.Attribute("Value") ?? element?.Value)?.Trim();
            }
        }

        public static string GetVersion() {
            using (var stream = typeof(ModInfo).Assembly
                .GetManifestResourceStream("BitulaMod.PublishConfiguration.xml")) {

                if (stream == null)
                    return null;

                return (string)XDocument.Load(stream).Root?
                    .Element("ModVersion")?.Attribute("Value");
            }
        }
    }
}
