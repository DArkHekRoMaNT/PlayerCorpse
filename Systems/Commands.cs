using CommonLib.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PlayerCorpse.Systems
{
    public class Commands : ModSystem
    {
        private ICoreServerAPI _sapi = null!;
        private DeathContentManager _deathContentManager = null!;

        public override void StartServerSide(ICoreServerAPI api)
        {
            _sapi = api;
            _deathContentManager = api.ModLoader.GetModSystem<DeathContentManager>();

            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands
                .Create("returnthings")
                .RequiresPrivilege(Core.Config.NeedPrivilegeForReturnThings)
                .WithDescription("Returns things lost at the last death")
                .BeginSubCommand("list")
                    .WithArgs(parsers.Player("player", api))
                    .HandleWith(ShowDeathList)
                .EndSubCommand()
                .BeginSubCommand("get")
                    .WithArgs(
                        parsers.Player("player", api),
                        parsers.Player("give to player", api),
                        parsers.OptionalInt("id", 0))
                    .HandleWith(ReturnThings)
                .EndSubCommand();
        }

        private TextCommandResult ShowDeathList(TextCommandCallingArgs args)
        {
            IPlayer player = (IPlayer)args[0];
            string[] files = _deathContentManager.GetDeathDataFiles(player);

            if (files.Length == 0)
            {
                return TextCommandResult.Error(Lang.Get("No data saved"));
            }

            var sb = new StringBuilder();
            for (int i = 0; i < files.Length; i++)
            {
                sb.AppendLine($"{i}. {Path.GetFileName(files[i])}");
            }
            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult ReturnThings(TextCommandCallingArgs args)
        {
            IPlayer player = (IPlayer)args[0];
            IPlayer giveToPlayer = (IPlayer)args[1];
            int id = (int)args[2];
            string[] files = _deathContentManager.GetDeathDataFiles(player);

            if (!_sapi.World.AllOnlinePlayers.Contains(giveToPlayer) || giveToPlayer.Entity == null)
            {
                return TextCommandResult.Error(Lang.Get(
                    "Player {0} is offline or not fully loaded.",
                    giveToPlayer.PlayerName));
            }

            if (id < 0 || files.Length <= id)
            {
                return TextCommandResult.Error(Lang.Get("Index {0} not found", id));
            }

            var dcm = _sapi.ModLoader.GetModSystem<DeathContentManager>();
            InventoryGeneric inventory = dcm.LoadLastDeathContent(player, id);
            foreach (var slot in inventory)
            {
                if (slot.Empty)
                {
                    continue;
                }

                if (!giveToPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                {
                    _sapi.World.SpawnItemEntity(slot.Itemstack, giveToPlayer.Entity.ServerPos.XYZ.AddCopy(0, 1, 0));
                }
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            return TextCommandResult.Success(Lang.Get(
                "Returned things from {0} to {1} with index {2}",
                player.PlayerName, giveToPlayer.PlayerName, id));
        }
    }
}
