using Colossal;
using System.Collections.Generic;

namespace BitulaMod.Locale {
    public class LocaleLeisureEN : IDictionarySource {
        private readonly Setting m_Setting;

        public LocaleLeisureEN(Setting setting) {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts) {

            indexCounts["BitulaMod.LIFEPATH_DoLeisure"] = 3;
            indexCounts["BitulaMod.LIFEPATH_WantNoLeisure"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoLeisureProvider"] = 1;
            indexCounts["BitulaMod.LIFEPATH_NoCityPark"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoMeals"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoCityIndoors"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoEntertainment"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoCommercial"] = 3;
            indexCounts["BitulaMod.LIFEPATH_NoCityBeach"] = 3;
            indexCounts["BitulaMod.LIFEPATH_ServiceFull"] = 1;
            indexCounts["BitulaMod.LIFEPATH_FoundLeisureAt"] = 1;
            indexCounts["BitulaMod.LIFEPATH_LeisureNoPath"] = 1;

            return new Dictionary<string, string> {
                { m_Setting.GetOptionTabLocaleID("LeisureSeeking"), "Leisure Seeking" },
                { m_Setting.GetOptionGroupLocaleID("LeisureSeeking"), "Leisure Seeking" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NormalLeisure)), "Normal Leisure" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NormalLeisure)), "Gives citizens a more realistic individual chance to choose leisure activities during their free time. Vanilla leisure behavior is gradually restored as the city grows, according to the configured milestone increment." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NormalLeisureMin)), "Minimum leisure desire" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NormalLeisureMin)), "Sets the minimum leisure desire percentage assigned to citizens using Small City leisure behavior." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NormalLeisureMax)), "Maximum leisure desire" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NormalLeisureMax)), "Sets the maximum leisure desire percentage assigned to citizens using Small City leisure behavior." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.LeisureIntervalMin)), "Minimum leisure duration" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.LeisureIntervalMin)), "Sets the minimum percentage of the available leisure time that citizens using Small City leisure behavior will spend at a leisure provider." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.LeisureIntervalMax)), "Maximum leisure duration" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.LeisureIntervalMax)), "Sets the maximum percentage of the available leisure time that citizens using Small City leisure behavior will spend at a leisure provider." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SpendingLeisureMin)), "Minimum leisure spending" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SpendingLeisureMin)), "Sets the minimum percentage of household money that citizens using Small City leisure behavior may spend during a leisure visit." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SpendingLeisureMax)), "Maximum leisure spending" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SpendingLeisureMax)), "Sets the maximum percentage of household money that citizens using Small City leisure behavior may spend during a leisure visit." },

                { "BitulaMod.LIFEPATH_DoLeisure:0", "I've earned a little fun today. Time to see what the city has to offer!" },
                { "BitulaMod.LIFEPATH_DoLeisure:1", "Enough with the daily routine! A little adventure sounds just right." },
                { "BitulaMod.LIFEPATH_DoLeisure:2", "Life isn't all work and chores. Let's find something enjoyable to do!" },

                { "BitulaMod.LIFEPATH_WantNoLeisure:0", "The world can have its fun without me today. I'm perfectly happy doing nothing." },
                { "BitulaMod.LIFEPATH_WantNoLeisure:1", "Going out sounds exhausting. I think I'll just enjoy a little peace and quiet." },
                { "BitulaMod.LIFEPATH_WantNoLeisure:2", "Not every free moment needs an adventure. Sometimes a quiet day is all I want." },

                { "BitulaMod.LIFEPATH_NoCityPark:0", "I wanted to get some fresh air in a park, but there wasn't one nearby." },
                { "BitulaMod.LIFEPATH_NoCityPark:1", "I felt like taking a walk through a park, but I couldn't find one." },
                { "BitulaMod.LIFEPATH_NoCityPark:2", "A quiet park would have been nice, but there wasn't anywhere suitable to go." },

                { "BitulaMod.LIFEPATH_NoLeisureProvider:0", "I wanted to do something for leisure, but I couldn't find a suitable place." },

                { "BitulaMod.LIFEPATH_NoMeals:0", "I was craving a proper meal, but nowhere nearby had anything for me." },
                { "BitulaMod.LIFEPATH_NoMeals:1", "I went out hoping to grab a bite, but I couldn't find anywhere to eat." },
                { "BitulaMod.LIFEPATH_NoMeals:2", "My stomach had plans, but apparently the city didn't." },

                { "BitulaMod.LIFEPATH_NoCityIndoors:0", "I was hoping for some culture or recreation, but nothing caught my interest." },
                { "BitulaMod.LIFEPATH_NoCityIndoors:1", "I felt like doing something cultural or active, but there wasn't much on offer." },
                { "BitulaMod.LIFEPATH_NoCityIndoors:2", "I was looking for somewhere to enjoy some culture, sport or recreation, but came up empty." },

                { "BitulaMod.LIFEPATH_NoEntertainment:0", "I was in the mood for some entertainment, but there was nowhere interesting to go." },
                { "BitulaMod.LIFEPATH_NoEntertainment:1", "I wanted a little fun, but the city didn't have anything that caught my attention." },
                { "BitulaMod.LIFEPATH_NoEntertainment:2", "I went looking for something entertaining to do, but apparently tonight's show was cancelled." },

                { "BitulaMod.LIFEPATH_NoCommercial:0", "I felt like going shopping, but there wasn't anywhere suitable to go." },
                { "BitulaMod.LIFEPATH_NoCommercial:1", "I wanted to browse the shops, but nothing around here caught my interest." },
                { "BitulaMod.LIFEPATH_NoCommercial:2", "I was in the mood to spend some money, but the city gave me nowhere to spend it." },

                { "BitulaMod.LIFEPATH_NoCityBeach:0", "I wanted to spend some time by the water, but there wasn't a suitable beach to go to." },
                { "BitulaMod.LIFEPATH_NoCityBeach:1", "I felt like relaxing at the beach, but I couldn't find anywhere to do it." },
                { "BitulaMod.LIFEPATH_NoCityBeach:2", "I was ready for sand, sun, and a little escape, but there was nowhere to put down a towel." },

                { "BitulaMod.LIFEPATH_ServiceFull:0", "I found a place I wanted to visit, but it was already full." },
                { "BitulaMod.LIFEPATH_FoundLeisureAt:0", "I found a place I wanted to visit. Let's go to {LINK_1}." },
                { "BitulaMod.LIFEPATH_LeisureNoPath:0", "I couldn't find a place I could reach in time." },
            };
        }

        public void Unload() {
        }
    }
}