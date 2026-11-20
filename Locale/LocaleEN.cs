using Colossal;
using System.Collections.Generic;

namespace BitulaMod.Locale {
    public class LocaleEN : IDictionarySource {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting) {
            m_Setting = setting;
        }

        private void SetMilestones(Dictionary<string, string> dictionary) {
            string[] villageMilestones = {
                "Tiny Hamlet", "Small Hamlet", "Hamlet", "Large Hamlet",
                "Tiny Village", "Small Village", "Large Village", "Grand Village",
                "Tiny Town", "Boom Town", "Busy Town", "Big Town", "Great Town",
                "Small City", "Big City", "Large City", "Huge City", "Grand City",
                "Metropolis", "Thriving Metropolis", "Flourishing Metropolis",
                "Expansive Metropolis", "Massive Metropolis", "Megalopolis"
            };

            for (int i = 0; i < villageMilestones.Length; i++) {
                dictionary[$"Progression.MILESTONE_NAME:{i + 1}"] = villageMilestones[i];
            }
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts) {

            indexCounts["BitulaMod.LIFEPATH_DebugMessage"] = 1;
            indexCounts["BitulaMod.LIFEPATH_StartedLookingForWork"] = 1;
            indexCounts["BitulaMod.LIFEPATH_DoesntLikeAnyJobs"] = 1;
            indexCounts["BitulaMod.LIFEPATH_NoJobsAvailable"] = 1;
            indexCounts["BitulaMod.LIFEPATH_StartedLookingForAnotherJob"] = 1;
            indexCounts["BitulaMod.LIFEPATH_TooFewBetterJobs"] = 1;
            indexCounts["BitulaMod.LIFEPATH_DoesntWantBetterJob"] = 1;
            indexCounts["BitulaMod.LIFEPATH_SwitchJobWorse"] = 1;
            indexCounts["BitulaMod.LIFEPATH_SwitchJobNone"] = 1;
            indexCounts["BitulaMod.LIFEPATH_SwitchJobNoTake"] = 1;
            indexCounts["BitulaMod.LIFEPATH_WorkplaceGone"] = 1;
            indexCounts["BitulaMod.LIFEPATH_EmployerGone"] = 1;
            indexCounts["BitulaMod.LIFEPATH_EmployerReturned"] = 1;
            indexCounts["BitulaMod.LIFEPATH_CantSwitchJob"] = 1;
            indexCounts["BitulaMod.LIFEPATH_NoSuitableSwitch"] = 1;
            indexCounts["BitulaMod.LIFEPATH_FoundCloserJob"] = 1;
            indexCounts["BitulaMod.LIFEPATH_PromotedJob"] = 1;
            indexCounts["BitulaMod.LIFEPATH_CloserJobUnemployed"] = 1;
            indexCounts["BitulaMod.LIFEPATH_CloserJobEmployed"] = 1;

            var dictionary = new Dictionary<string, string> {
                { m_Setting.GetSettingsLocaleID(), "Village Life" },
                { m_Setting.GetOptionTabLocaleID("General"), "General" },
                { m_Setting.GetOptionTabLocaleID("JobSeeking"), "Job Seeking" },
                { m_Setting.GetOptionGroupLocaleID("General"), "General" },
                { m_Setting.GetOptionGroupLocaleID("JobSeeking"), "Job Seeking" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.JobSeekerMilestoneText)), "Population milestone" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.JobSeekerMilestoneText)), "Population at which the chance of applying Village Life behaviour starts to decrease." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.JobSeekerFailureIncrement)), "Behaviour reduction increment" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.JobSeekerFailureIncrement)), "Percentage points by which the chance of applying Village Life behaviour decreases at each population milestone." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.VillageProgression)), "Village progression" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.VillageProgression)), "Enables Village Life's custom milestone progression, including additional milestones and adjusted unlock requirements. Requires restarting the game to take effect." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FullTrafficSimulation)), "Full Traffic Simulation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FullTrafficSimulation)), "Disables population-based traffic reduction, allowing more citizen trips to be simulated physically. May significantly increase traffic and reduce performance in larger cities." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ProgressiveTrafficSimulation)), "Progressive Traffic Simulation" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ProgressiveTrafficSimulation)), "Gradually reintroduces the game's traffic reduction as the city grows. Small cities begin with no traffic reduction, allowing all simulated traffic to appear normally. At each configured population milestone, an additional percentage of the vanilla traffic reduction is restored according to the Job Seeker Failure Increment setting, until the original vanilla value is reached. Unlike Full Traffic Simulation, which keeps traffic reduction completely disabled regardless of population, this option progressively returns traffic simulation toward vanilla behavior as the city becomes larger." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.PrioritizeAdultEmployment)), "Prioritize adult employment" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.PrioritizeAdultEmployment)), "Prioritizes employment over further education for adults in small cities. This effect gradually decreases as the population grows and eventually returns to the original education behavior." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AcceptLowerJobs)), "Accept lower jobs" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AcceptLowerJobs)), "Allows citizens to accept jobs below their education level more readily in smaller cities." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AcceptJobSwitch)), "Accept job switch" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AcceptJobSwitch)), "Allow employed citizens to switch to better jobs more readily in small cities. The effect is based on the Job Seeker Milestone and gradually returns to vanilla behavior as the city grows, while preventing excessive job hopping between positions of the same level." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReducedDaysOff)), "Less Days Off" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ReducedDaysOff)), "Reduces worker days off in small cities, gradually returning to vanilla behavior as the population grows." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.WorkplacePromotion)), "Workplace Promotion" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.WorkplacePromotion)), "Allow workers to be promoted to a higher-level position at their current workplace when a suitable position is available." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CloserJobUnemployed)), "Unemployed seek closest job" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CloserJobUnemployed)), "Allows unemployed citizens to choose the closest suitable available job." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CloserJobEmployed)), "Employed switch to closer job" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CloserJobEmployed)), "Allows employed citizens to switch to a suitable job that is closer to home than their current job." },

                { "BitulaMod.LIFEPATH_DebugMessage:0", "DEBUG: {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_LINK_DebugMessage", "{0}" },

                { "BitulaMod.LIFEPATH_StartedLookingForWork:0", "I Started looking for work. {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_LINK_StartedLookingForWork", "I found {0} suitable positions of which {1} matches my education level." },

                { "BitulaMod.LIFEPATH_NoJobsAvailable:0", "There are no suitable open job positions in this city." },
                { "BitulaMod.LIFEPATH_DoesntLikeAnyJobs:0", "I don't like any of the open job positions." },

                { "BitulaMod.LIFEPATH_StartedLookingForAnotherJob:0", "I started looking for a better job. {LINK_NAME_1}" },
                { "BitulaMod.LIFEPATH_LINK_StartedLookingForAnotherJob", "I found {0} suitable positions." },

                { "BitulaMod.LIFEPATH_TooFewBetterJobs:0", "{LINK_NAME_1}, but that's too few to make looking for another job worthwhile." },
                { "BitulaMod.LIFEPATH_LINK_TooFewBetterJobs", "I found {0} suitable positions" },

                { "BitulaMod.LIFEPATH_DoesntWantBetterJob:0", "Some of these are better jobs, but I don't want to change jobs right now." },
                { "BitulaMod.LIFEPATH_SwitchJobWorse:0", "I looked for another job, but all the available ones are worse than my current job." },
                { "BitulaMod.LIFEPATH_SwitchJobNone:0", "I looked for another job, but there aren't any available jobs right now." },
                { "BitulaMod.LIFEPATH_SwitchJobNoTake:0", "I found another job, but I'm not qualified enough to take it." },
                { "BitulaMod.LIFEPATH_WorkplaceGone:0", "Looks like my workplace is gone. I'll need to look for another job soon." },
                { "BitulaMod.LIFEPATH_EmployerGone:0", "Looks like my employer is gone. I'll need to look for another job soon." },
                { "BitulaMod.LIFEPATH_EmployerReturned:0", "Looks like my employer is back." },
                { "BitulaMod.LIFEPATH_CantSwitchJob:0", "I would like a better job, but there are no suitable positions available." },
                { "BitulaMod.LIFEPATH_NoSuitableSwitch:0", "There are other suitable positions available, but none would be a better or closer job." },
                { "BitulaMod.LIFEPATH_FoundCloserJob:0", "I found a job closer to home than my current workplace." },
                { "BitulaMod.LIFEPATH_PromotedJob:0", "I was promoted to a better position at my workplace." },
            };

            if (m_Setting.VillageProgression)
                SetMilestones(dictionary);

            return dictionary;
        }

        public void Unload() {
        }
    }
}