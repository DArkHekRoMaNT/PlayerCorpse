using System;
using System.IO;
using System.Linq;
using System.Text;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace PlayerCorpse
{
    public class Core : ModSystem
    {
        ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            this.api = api;

            Config.Current = api.LoadOrCreateConfig<Config>(ConstantsCore.ModId + ".json");
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

            string returnthings_help = "/returnthings [from player] [to player] [index] or /returnthings [from player] list";
            api.RegisterCommand("returnthings",
                ConstantsCore.ModPrefix + "Returns things losing on last death", returnthings_help,
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    if (args.Length < 2)
                    {
                        player.SendMessage(returnthings_help);
                        return;
                    }

                    IPlayer fromPlayer = api.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == args[0].ToLower());
                    if (fromPlayer == null)
                    {
                        player.SendMessage(Lang.Get("Player {0} not found", args[0]));
                        return;
                    }

                    string datapath = api.GetOrCreateDataPath($"ModData/{api.GetWorldId()}/{ConstantsCore.ModId}/{player.PlayerUID}");
                    string[] files = Directory.GetFiles(datapath).OrderByDescending(f => new FileInfo(f).Name).ToArray();

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
                            str.AppendLine($"{i}. {Path.GetFileName(files[i])}");
                        }
                        player.SendMessage(str.ToString());
                        return;
                    }

                    IPlayer toPlayer = api.World.AllPlayers.FirstOrDefault(p => p.PlayerName.ToLower() == args[1].ToLower());
                    if (toPlayer == null)
                    {
                        player.SendMessage(Lang.Get("Player {0} not found", args[1]));
                        return;
                    }

                    if (!api.World.AllOnlinePlayers.Contains(toPlayer) || toPlayer.Entity == null)
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

                    InventoryGeneric inventory = LoadLastDeathContent(player, offset);
                    foreach (var slot in inventory)
                    {
                        if (slot.Empty) continue;

                        if (!player.InventoryManager.TryGiveItemstack(slot.Itemstack))
                        {
                            api.World.SpawnItemEntity(slot.Itemstack, player.Entity.ServerPos.XYZ.AddCopy(0, 1, 0));
                        }
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }
                },
                Config.Current.NeedPrivilegeForReturnThings.Val
            );
        }

        internal static void SaveDeathContent(InventoryGeneric inventory, IPlayer player)
        {
            ICoreAPI api = player.Entity?.Api;
            if (api == null) throw new NullReferenceException("player.Entity.api is null");

            string datapath = api.GetOrCreateDataPath($"ModData/{api.GetWorldId()}/{ConstantsCore.ModId}/{player.PlayerUID}");
            string[] files = Directory.GetFiles(datapath).OrderByDescending(f => new FileInfo(f).Name).ToArray();

            for (int i = files.Count() - 1; i > Config.Current.MaxDeathContentSavedPerPlayer.Val - 2; i--)
            {
                File.Delete(files[i]);
            }

            TreeAttribute tree = new TreeAttribute();
            inventory.ToTreeAttributes(tree);

            string name = "inventory-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".dat";
            File.WriteAllBytes($"{datapath}/{name}", tree.ToBytes());
        }

        internal static InventoryGeneric LoadLastDeathContent(IPlayer player, int offset = 0)
        {
            ICoreAPI api = player.Entity?.Api;
            if (api == null) throw new NullReferenceException("player.Entity.api is null");
            if (Config.Current.MaxDeathContentSavedPerPlayer.Val <= offset) throw new IndexOutOfRangeException("offset is too large or save data disabled");

            string datapath = api.GetOrCreateDataPath($"ModData/{api.GetWorldId()}/{ConstantsCore.ModId}/{player.PlayerUID}");
            string file = Directory.GetFiles(datapath).OrderByDescending(f => new FileInfo(f).Name).ToArray().ElementAt(offset);

            TreeAttribute tree = new TreeAttribute();
            tree.FromBytes(File.ReadAllBytes(file));

            InventoryGeneric inv = new InventoryGeneric(tree.GetInt("qslots"), "playercorpse-" + player.PlayerUID, api);
            inv.FromTreeAttributes(tree);
            return inv;
        }
    }
}