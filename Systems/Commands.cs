using CommonLib.Extensions;
using CommonLib.Utils;
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
        private const string ReturnThingsHelp =
            "/returnthings [from player] [to player] [index] or /returnthings [from player] list";

        private ICoreServerAPI sapi = null!;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.RegisterCommand("returnthings",
                "[" + Core.ModId + "] Returns things losing on last death", ReturnThingsHelp,
                ReturnThingsCommand,
                Config.Current.NeedPrivilegeForReturnThings.Value
            );
        }

        private void ReturnThingsCommand(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length < 2)
            {
                player.SendMessage(ReturnThingsHelp);
                return;
            }


            IPlayer fromPlayer = sapi.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == args[0].ToLower());
            if (fromPlayer == null)
            {
                player.SendMessage(Lang.Get("Player {0} not found", args[0]));
                return;
            }

            string localPath = Path.Combine("ModData", sapi.GetWorldId(), Mod.Info.ModID, fromPlayer.PlayerUID);
            string path = sapi.GetOrCreateDataPath(localPath);
            string[] files = Directory.GetFiles(path).OrderByDescending(f => new FileInfo(f).Name).ToArray();

            if (args[1] == "list")
            {
                if (files.Length == 0)
                {
                    player.SendMessage(Lang.Get("No data saved"));
                    return;
                }

                StringBuilder str = new StringBuilder();
                for (int i = 0; i < files.Length; i++)
                {
                    str.AppendLine(i + ". " + Path.GetFileName(files[i]));
                }
                player.SendMessage(str.ToString());
                return;
            }

            IPlayer toPlayer = sapi.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == args[1].ToLower());
            if (toPlayer == null)
            {
                player.SendMessage(Lang.Get("Player {0} not found", args[1]));
                return;
            }

            if (!sapi.World.AllOnlinePlayers.Contains(toPlayer) || toPlayer.Entity == null)
            {
                player.SendMessage(Lang.Get("Player {0} is offline or not fully loaded.", args[1]));
                return;
            }

            int offset = args.Length > 2 ? args[2].ToInt(-1) : 0;
            if (offset == -1 || files.Length <= offset)
            {
                player.SendMessage(Lang.Get("Index {0} not found", args.Length > 2 ? args[2] : offset.ToString()));
                return;
            }

            var dcm = sapi.ModLoader.GetModSystem<DeathContentManager>();
            InventoryGeneric inventory = dcm.LoadLastDeathContent(fromPlayer, offset);
            foreach (var slot in inventory)
            {
                if (slot.Empty) continue;

                if (!toPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                {
                    sapi.World.SpawnItemEntity(slot.Itemstack, toPlayer.Entity.ServerPos.XYZ.AddCopy(0, 1, 0));
                }
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            player.SendMessage(Lang.Get("Returned things from {0} to {1} with index {2}", fromPlayer.PlayerName, toPlayer.PlayerName, offset));
        }
    }
}
