using CommonLib.Extensions;
using System;
using System.IO;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PlayerCorpse
{
    public class DeathContentManager : ModSystem
    {
        public void SaveDeathContent(InventoryGeneric inventory, IPlayer player)
        {
            ICoreAPI api = player.Entity.Api;

            string localPath = "ModData/" + api.GetWorldId() + "/" + Mod.Info.ModID + "/" + player.PlayerUID;
            string path = api.GetOrCreateDataPath(localPath);
            string[] files = Directory.GetFiles(path).OrderByDescending(f => new FileInfo(f).Name).ToArray();

            for (int i = files.Length - 1; i > Config.Current.MaxDeathContentSavedPerPlayer.Value - 2; i--)
            {
                File.Delete(files[i]);
            }

            TreeAttribute tree = new TreeAttribute();
            inventory.ToTreeAttributes(tree);

            string name = "inventory-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".dat";
            File.WriteAllBytes(path + "/" + name, tree.ToBytes());
        }

        public InventoryGeneric LoadLastDeathContent(IPlayer player, int offset = 0)
        {
            ICoreAPI api = player.Entity.Api;
            if (Config.Current.MaxDeathContentSavedPerPlayer.Value <= offset)
                throw new IndexOutOfRangeException("offset is too large or save data disabled");

            string localPath = "ModData/" + api.GetWorldId() + "/" + Mod.Info.ModID + "/" + player.PlayerUID;
            string path = api.GetOrCreateDataPath(localPath);
            string file = Directory.GetFiles(path).OrderByDescending(f => new FileInfo(f).Name).ToArray().ElementAt(offset);

            TreeAttribute tree = new TreeAttribute();
            tree.FromBytes(File.ReadAllBytes(file));

            InventoryGeneric inv = new InventoryGeneric(tree.GetInt("qslots"), "playercorpse-" + player.PlayerUID, api);
            inv.FromTreeAttributes(tree);
            return inv;
        }
    }
}