using UnityEngine;


namespace BitulaMod {
    public struct CustomMilestone {
        public string Name;
        public int Index;
        public int XpRequired;
        public int Reward;
        public int DevTreePoints;
        public int MapTiles;
        public int LoanLimit;
        public bool Major;
        public bool IsVictory;

        public string Image;
        public Color BackgroundColor;
        public Color AccentColor;
        public Color TextColor;
        public static CustomMilestone MakeHamlet() {
            return new CustomMilestone {
                Name = "Hamlet",
                Index = 1,
                XpRequired = 80,
                Reward = 0,
                DevTreePoints = 0,
                MapTiles = 1,
                LoanLimit = 0,
                Major = false,
                IsVictory = false,

                Image = "coui://ui-mods/images/Hamlet.svg",
                BackgroundColor = new Color(0.93f, 0.76f, 0.62f, 1f),
                AccentColor = new Color(0.65f, 0.80f, 0.55f, 1f),
                TextColor = new Color(0.18f, 0.16f, 0.14f, 1f)
            };
        }
    }


}
