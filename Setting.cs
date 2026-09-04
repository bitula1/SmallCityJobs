using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;
using System;

namespace BitulaMod
{
    [FileLocation(nameof(BitulaMod))]
    public class Setting : ModSetting
    {

        public Setting(IMod mod) : base(mod)
        {
        }

        public int JobSeekerMilestone = 100;

        [SettingsUISection("JobSeeking")]
        [SettingsUITextInput]
        public string JobSeekerMilestoneText
        {
            get => JobSeekerMilestone.ToString();

            set
            {
                if (int.TryParse(value, out int parsed))
                {
                    JobSeekerMilestone = Math.Max(1, parsed);
                }
            }
        }

        [SettingsUISection("JobSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int JobSeekerFailureIncrement { get; set; } = 10;

        [SettingsUISection("JobSeeking")]
        public bool PrioritizeAdultEmployment { get; set; } = true;

        [SettingsUISection("JobSeeking")]
        public bool AcceptLowerJobs { get; set; } = true;



        public override void SetDefaults()
        {
            JobSeekerMilestoneText = "100";
            JobSeekerFailureIncrement = 10;
            PrioritizeAdultEmployment = true;
            AcceptLowerJobs = true;
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {

            indexCounts["BitulaMod.LIFEPATH_StartedLookingForWork"] = 1;
            indexCounts["BitulaMod.LIFEPATH_NoJobsAvailable"] = 1;
            indexCounts["BitulaMod.LIFEPATH_DoesntLikeAnyJobs"] = 1;
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Small City Jobs" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.JobSeekerMilestoneText)), "Job-seeker population milestone" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.JobSeekerMilestoneText)), "Population interval at which the job-application failure chance increases." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.JobSeekerFailureIncrement)), "Failure chance increment" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.JobSeekerFailureIncrement)), "Failure percentage points added at each population milestone." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PrioritizeAdultEmployment)), "Prioritize adult employment" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PrioritizeAdultEmployment)), "Prioritizes employment over further education for adults in small cities. This effect gradually decreases as the population grows and eventually returns to the original education behavior." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AcceptLowerJobs)), "Accept lower jobs" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AcceptLowerJobs)), "Allows citizens to accept jobs below their education level more readily in smaller cities." },
                { "BitulaMod.LIFEPATH_StartedLookingForWork:0", "I Started looking for work. {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_NoJobsAvailable:0", "There are no open job positions in this city." },
                { "BitulaMod.LIFEPATH_DoesntLikeAnyJobs:0", "I don't like any of the open job positions." },
                { "BitulaMod.LIFEPATH_LINK_StartedLookingForWork:0", "I found {0} suitable positions of which {1} matches my education level." }

            };
        }

        public void Unload()
        {

        }
    }
}
