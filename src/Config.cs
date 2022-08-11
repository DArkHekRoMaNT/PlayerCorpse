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
            public T Value
            {
                get { return val != null ? val : val = Default; }
                set { val = value != null ? value : Default; }
            }
            public Part(T Default, string Comment = null)
            {
                this.Default = Default;
                this.Value = Default;
                this.Comment = Comment;
            }
            public Part(T Default, string prefix, string[] allowed, string postfix = null)
            {
                this.Default = Default;
                this.Value = Default;
                this.Comment = prefix;

                this.Comment += "[" + allowed[0];
                for (int i = 1; i < allowed.Length; i++)
                {
                    this.Comment += ", " + allowed[i];
                }
                this.Comment += "]" + postfix;
            }
        }

        public Part<bool> CanFired { get; set; }
        public Part<bool> HasHealth { get; set; }
        public Part<bool> CreateCorpse { get; set; }
        public Part<string[]> SaveInventoryTypes { get; set; }
        public Part<string> NeedPrivilegeForReturnThings { get; set; }
        public Part<int> MaxDeathContentSavedPerPlayer { get; set; }
        public Part<string> CreateWaypoint { get; set; }
        public Part<string> WaypointIcon { get; set; }
        public Part<string> WaypointColor { get; set; }
        public Part<bool> PinWaypoint { get; set; }
        public Part<bool> DebugMode { get; set; }
        public Part<int> FreeCorpseAfterTime { get; set; }
        public Part<float> CorpseCollectionTime { get; set; }
        public Part<bool> CorpseCompassEnabled { get; set; }

        public Config()
        {
            CanFired = new Part<bool>(false, "Burns in 15 seconds. LET EVERYTHING BURN IN LAVA!");
            HasHealth = new Part<bool>(false, "Has 100 hp, can be broken by another player");
            CreateCorpse = new Part<bool>(true, "[true, false]");
            SaveInventoryTypes = new Part<string[]>(new string[]{
                GlobalConstants.hotBarInvClassName,
                GlobalConstants.backpackInvClassName,
                GlobalConstants.craftingInvClassName,
                GlobalConstants.mousecursorInvClassName,
                GlobalConstants.characterInvClassName
            });
            NeedPrivilegeForReturnThings = new Part<string>(Privilege.gamemode, "", Privilege.AllCodes());
            MaxDeathContentSavedPerPlayer = new Part<int>(10, "[0-100] (0 - disabled, more than 100 - not recommended)");
            CreateWaypoint = new Part<string>("auto", "[auto, always, none], auto - will try to resolve conflicts with other mods");
            WaypointIcon = new Part<string>("bee", "[circle, bee, cave, home, ladder, pick, rocks, ruins, spiral, star1, star2, trader, vessel]");
            WaypointColor = new Part<string>("crimson", "https://www.99colors.net/dot-net-colors");
            PinWaypoint = new Part<bool>(true, "[true, false]");
            DebugMode = new Part<bool>(false, "[true, false]");
            FreeCorpseAfterTime = new Part<int>(240, "[Any integer] (0 - always, below zero - never), makes corpses available to everyone after N in-game hours");
            CorpseCollectionTime = new Part<float>(1, "[float], Corpse collection time in seconds");
            CorpseCompassEnabled = new Part<bool>(true);
        }
    }
}