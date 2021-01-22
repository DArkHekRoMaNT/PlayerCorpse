using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class Config
    {
        public static Config Current { get; set; }
        public class Part<T>
        {
            public readonly string Comment;
            public readonly T Default;
            private T val;
            public T Val
            {
                get => (val != null ? val : val = Default);
                set => val = (value != null ? value : Default);
            }
            public Part(T Default, string Comment = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = Comment;
            }
            public Part(T Default, string prefix, string[] allowed, string postfix = null)
            {
                this.Default = Default;
                this.Val = Default;
                this.Comment = prefix;

                this.Comment += "[" + allowed[0];
                for (int i = 1; i < allowed.Length; i++)
                {
                    this.Comment += ", " + allowed[i];
                }
                this.Comment += "]" + postfix;
            }
        }

        public Part<bool> CanFired { get; set; } = new Part<bool>(false, "Burns in 15 seconds. LET EVERYTHING BURN IN LAVA!");
        public Part<bool> HasHealth { get; set; } = new Part<bool>(false, "Has 100 hp, can be broken by another player");
        public Part<bool> CreateCorpse { get; set; } = new Part<bool>(true, "[true, false]");
        public Part<string[]> SaveInventoryTypes { get; set; } = new Part<string[]>(new string[]{
            GlobalConstants.hotBarInvClassName,
            GlobalConstants.backpackInvClassName,
            GlobalConstants.craftingInvClassName,
            GlobalConstants.mousecursorInvClassName,
            GlobalConstants.characterInvClassName
        });
        public Part<string> NeedPrivilegeForReturnThings { get; set; } = new Part<string>(Privilege.gamemode, "", Privilege.AllCodes());

        public Part<int> MaxDeathContentSavedPerPlayer { get; set; } = new Part<int>(10, "[0-100] (0 - disabled, more than 100 - not recommended)");
        public Part<string> CreateWaypoint { get; set; } = new Part<string>("auto", "[auto, always, none], auto - will try to resolve conflicts with other mods");
        public Part<string> WaypointIcon { get; set; } = new Part<string>("bee", "[circle, bee, cave, home, ladder, pick, rocks, ruins, spiral, star1, star2, trader, vessel]");
        public Part<string> WaypointColor { get; set; } = new Part<string>("crimson", "https://www.99colors.net/dot-net-colors");
        public Part<bool> PinWaypoint { get; set; } = new Part<bool>(true, "[true, false]");
    }
}