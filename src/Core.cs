using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class Core : ModSystem
    {
        ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            Config.Current = api.LoadOrCreateConfig<Config>(Constants.MOD_ID + ".json");
            if (Config.Current.CreateWaypoint.Val == "auto")
            {
                Config.Current.CreateWaypoint.Val = api.ModLoader.IsModEnabled("streamdc") ? "none" : "always";
            }
            api.RegisterEntityBehaviorClass("playercorpseondeath", typeof(EntityBehaviorPlayerCorpseOnDeath));
            api.RegisterEntity("EntityPlayerCorpse", typeof(EntityPlayerCorpse));
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            api.RegisterCommand("pc_returnthings", "Returns things losing on last death", "/retunrthings [from player] [to player] [earlier on num]",
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    player.SendMessage("WIP");
                }, Config.Current.NeedPrivilegeForReturnThings.Val);
        }
    }
}