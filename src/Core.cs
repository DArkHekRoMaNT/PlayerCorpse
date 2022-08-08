using SharedUtils.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PlayerCorpse
{
    public class Core : ModSystem
    {
        public static string ModId { get; private set; }
        public static ILogger ModLogger { get; private set; }

        public HudCircleRenderer InteractRingRenderer { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            ModId = Mod.Info.ModID;
            ModLogger = Mod.Logger;
        }

        public override void Start(ICoreAPI api)
        {
            Config.Current = api.LoadOrCreateConfig<Config>(Mod.Info.ModID + ".json");

            api.World.Config.SetBool(Mod.Info.ModID + ":CorpseCompassEnabled", Config.Current.CorpseCompassEnabled.Val);

            api.RegisterEntityBehaviorClass("playercorpseondeath", typeof(EntityBehaviorPlayerCorpseOnDeath));
            api.RegisterEntity("EntityPlayerCorpse", typeof(EntityPlayerCorpse));
            api.RegisterItemClass("ItemCorpseCompass", typeof(ItemCorpseCompass));
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            if (Config.Current.CreateWaypoint.Val == "auto")
            {
                Config.Current.CreateWaypoint.Val = "always";

                string[] hasDeathWaypointsMods = api.Assets.Get<string[]>(
                    new AssetLocation(Mod.Info.ModID, "config/hasdeathwaypointsmods.json"));

                foreach (string modid in hasDeathWaypointsMods)
                {
                    if (api.ModLoader.IsModEnabled(modid))
                    {
                        Config.Current.CreateWaypoint.Val = "none";
                    }
                }
            }
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            InteractRingRenderer = new HudCircleRenderer(api, new HudCircleSettings()
            {
                Color = 0xFF9500
            });
        }
    }
}