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
    [SettingsUITabOrder("JobSeeking", "LeisureSeeking")]
    public class Setting : ModSetting
    {

        public Setting(IMod mod) : base(mod)
        {
        }



        public int JobSeekerMilestone = 500;

        private string m_JobSeekerMilestoneText = "500";

        [SettingsUISection("General", "General")]
        [SettingsUITextInput]
        public string JobSeekerMilestoneText {
            get => m_JobSeekerMilestoneText ?? JobSeekerMilestone.ToString();

            set {
                m_JobSeekerMilestoneText = value;

                if (int.TryParse(value, out int parsed)) {
                    JobSeekerMilestone = Math.Max(1, parsed);
                }
            }
        }

        [SettingsUISection("General", "General")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int JobSeekerFailureIncrement { get; set; } = 10;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool PrioritizeAdultEmployment { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool AcceptLowerJobs { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool AcceptJobSwitch { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool FullTrafficSimulation { get; set; } = false;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool ProgressiveTrafficSimulation { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool ReducedDaysOff { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool WorkplacePromotion { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool CloserJobEmployed { get; set; } = true;

        [SettingsUISection("JobSeeking", "JobSeeking")]
        public bool CloserJobUnemployed { get; set; } = true;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        public bool NormalLeisure { get; set; } = true;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int NormalLeisureMin { get; set; } = 5;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int NormalLeisureMax { get; set; } = 46;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int LeisureIntervalMin { get; set; } = 20;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int LeisureIntervalMax { get; set; } = 80;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int SpendingLeisureMin { get; set; } = 5;

        [SettingsUISection("LeisureSeeking", "LeisureSeeking")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int SpendingLeisureMax { get; set; } = 50;

        
        [SettingsUISection("Shopping", "Shopping")]
        public bool NormalShopping { get; set; } = true;        

        [SettingsUISection("Shopping", "Shopping")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]
        public int ShoppingIntervalMin { get; set; } = 30;

        [SettingsUISection("Shopping", "Shopping")]
        [SettingsUISlider(min = 0, max = 100, step = 1, unit = Unit.kPercentage)]

        [SettingsUISection("Shopping", "Shopping")]
        public bool PhysicalVirtualShopping { get; set; } = true;

        public int ShoppingIntervalMax { get; set; } = 60;

        [SettingsUISection("Shopping", "Shopping")]
        [SettingsUIDropdown(typeof(Setting), nameof(GetShoppingMaxAttemptsItems))]
        public int ShoppingMaxFailedAttempts { get; set; } = 3;

        public DropdownItem<int>[] GetShoppingMaxAttemptsItems() {
            int[] values = { 1, 2, 3, 4, 5, 7, 10, 20, 40 };
            DropdownItem<int>[] items = new DropdownItem<int>[values.Length];

            for (int i = 0; i < values.Length; i++) {
                items[i] = new DropdownItem<int> {
                    value = values[i],
                    displayName = values[i].ToString()
                };
            }

            return items;
        }

        [SettingsUISection("General", "General")]
        public bool VillageProgression { get; set; } = true;

        public override void SetDefaults()
        {
            JobSeekerMilestoneText = "500";
            JobSeekerFailureIncrement = 10;
            PrioritizeAdultEmployment = true;
            AcceptLowerJobs = true;
            AcceptJobSwitch = true;
            ReducedDaysOff = true;
            FullTrafficSimulation = false;
            ProgressiveTrafficSimulation = true;
            WorkplacePromotion = true;
            CloserJobEmployed = true;
            CloserJobUnemployed = true;
            PhysicalVirtualShopping = true;
            VillageProgression = true;

            NormalLeisure = true;
            NormalLeisureMin = 5;
            NormalLeisureMax = 46;
            LeisureIntervalMin = 20;
            LeisureIntervalMax = 80;
            SpendingLeisureMin = 5;
            SpendingLeisureMax = 50;

            NormalShopping = true;
            ShoppingIntervalMin = 30;
            ShoppingIntervalMax = 60;
            ShoppingMaxFailedAttempts = 3;
        }
    }
}
