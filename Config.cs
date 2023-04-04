using CommonLib.Config;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    [Config("playercorpse.json")]
    public class Config
    {
        [Description("Burns in 15 seconds. LET EVERYTHING BURN IN LAVA!")]
        public bool CanFired { get; set; } = false;

        [Description("Has 100 hp, can be broken by another player")]
        public bool HasHealth { get; set; } = false;

        public bool CreateCorpse { get; set; } = true;

        public string[] SaveInventoryTypes { get; set; } = new string[]
        {
            GlobalConstants.hotBarInvClassName,
            GlobalConstants.backpackInvClassName,
            GlobalConstants.craftingInvClassName,
            GlobalConstants.mousecursorInvClassName,
            GlobalConstants.characterInvClassName
        };

        [Privileges]
        public string NeedPrivilegeForReturnThings { get; set; } = Privilege.gamemode;

        [Range(0, int.MaxValue)]
        public int MaxDeathContentSavedPerPlayer { get; set; } = 10;

        [Description("auto mode will try to resolve conflicts with other mods")]
        [Strings("auto", "always", "none")]
        public string CreateWaypoint { get; set; } = "auto";

        [Description("circle, bee, cave, home, ladder, pick, rocks, ruins, spiral, star1, star2, trader, vessel, etc")]
        public string WaypointIcon { get; set; } = "bee";

        [Description("https://www.99colors.net/dot-net-colors")]
        public string WaypointColor { get; set; } = "crimson";

        public bool PinWaypoint { get; set; } = true;
        public bool DebugMode { get; set; } = false;

        [Description("Makes corpses available to everyone after N in-game hours (0 - always, below zero - never)")]
        public int FreeCorpseAfterTime { get; set; } = 240;

        [Description("Corpse collection time in seconds")]
        public float CorpseCollectionTime { get; set; } = 1;

        [Description("If you set it to false, all already existing compasses will turn into an unknown item")]
        public bool CorpseCompassEnabled { get; set; } = true;
    }
}
