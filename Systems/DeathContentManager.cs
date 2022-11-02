using CommonLib.Extensions;
using CommonLib.Utils;
using PlayerCorpse.Entities;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse.Systems
{
    public class DeathContentManager : ModSystem
    {
        private ICoreServerAPI sapi = null!;

        public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            api.Event.OnEntityDeath += OnEntityDeath;
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (entity is EntityPlayer entityPlayer)
            {
                OnPlayerDeath((IServerPlayer)entityPlayer.Player, damageSource);
            }
        }

        private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            bool isKeepContent = byPlayer.Entity?.Properties?.Server?.Attributes?.GetBool("keepContents") ?? false;
            if (isKeepContent)
            {
                return;
            }

            var corpseEntity = CreateCorpseEntity(byPlayer);
            if (corpseEntity.Inventory != null && !corpseEntity.Inventory.Empty)
            {
                if (Config.Current.CreateWaypoint.Value == "always")
                {
                    CreateDeathPoint(byPlayer);
                }

                // Save content for /returnthings
                if (Config.Current.MaxDeathContentSavedPerPlayer.Value > 0)
                {
                    SaveDeathContent(corpseEntity.Inventory, byPlayer);
                }

                // Spawn corpse
                if (Config.Current.CreateCorpse.Value)
                {
                    sapi.World.SpawnEntity(corpseEntity);

                    string message = string.Format(
                        "Created {0} at {1}, id {2}",
                        corpseEntity.GetName(),
                        corpseEntity.SidedPos.XYZ.RelativePos(sapi),
                        corpseEntity.EntityId);

                    Core.ModLogger.Notification(message);
                    if (Config.Current.DebugMode.Value)
                    {
                        sapi.SendMessageToAll(message);
                    }
                }
                // Or drop all if corpse creations is disabled
                else
                {
                    corpseEntity.Inventory.DropAll(corpseEntity.Pos.XYZ);
                }
            }
            else
            {
                string message = string.Format(
                    "Inventory is empty, {0}'s corpse not created",
                    corpseEntity.OwnerName);

                Core.ModLogger.Notification(message);
                if (Config.Current.DebugMode.Value)
                {
                    sapi.SendMessageToAll(message);
                }
            }
        }

        private EntityPlayerCorpse CreateCorpseEntity(IServerPlayer byPlayer)
        {
            var entityType = sapi.World.GetEntityType(new AssetLocation(Core.ModId, "playercorpse"));

            if (sapi.World.ClassRegistry.CreateEntity(entityType) is not EntityPlayerCorpse corpse)
            {
                throw new Exception("Unable to instantiate player corpse");
            }

            corpse.OwnerUID = byPlayer.PlayerUID;
            corpse.OwnerName = byPlayer.PlayerName;
            corpse.CreationTime = sapi.World.Calendar.TotalHours;
            corpse.CreationRealDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            corpse.Inventory = TakeContentFromPlayer(byPlayer);

            // Fix dancing corpse issue
            Vec3i floorPos = TryFindFloor(byPlayer.Entity.ServerPos.XYZInt);

            // Attempt to align the corpse to the center of the block so that it does not crawl higher
            Vec3d pos = floorPos.ToVec3d().Add(.5, 0, .5);

            corpse.ServerPos.SetPos(pos);
            corpse.Pos.SetPos(pos);
            corpse.World = sapi.World;

            return corpse;
        }

        /// <summary> Try to find the nearest block with collision below </summary>
        private Vec3i TryFindFloor(Vec3i pos)
        {
            for (int i = pos.Y; i > 0; i--)
            {
                var block = sapi.World.BlockAccessor.GetBlock(pos.X, i, pos.Z);
                if (block.BlockId != 0 && block.CollisionBoxes?.Length > 0)
                {
                    return new Vec3i(pos.X, i + 1, pos.Z);
                }
            }

            return pos;
        }

        private InventoryGeneric TakeContentFromPlayer(IServerPlayer byPlayer)
        {
            var inv = new InventoryGeneric(GetMaxCorpseSlots(byPlayer), "playercorpse-" + byPlayer.PlayerUID, sapi);

            int lastSlotId = 0;
            foreach (var invClassName in Config.Current.SaveInventoryTypes.Value)
            {
                // Skip armor if it does not drop after death
                bool isDropArmor = byPlayer.Entity.Properties.Server?.Attributes?.GetBool("dropArmorOnDeath") ?? false;
                if (invClassName == GlobalConstants.characterInvClassName && !isDropArmor)
                {
                    continue;
                }

                foreach (var slot in byPlayer.InventoryManager.GetOwnInventory(invClassName))
                {
                    inv[lastSlotId++].Itemstack = TakeSlotContent(slot);
                }
            }

            return inv;
        }

        private static int GetMaxCorpseSlots(IServerPlayer byPlayer)
        {
            int maxCorpseSlots = 0;
            foreach (var invClassName in Config.Current.SaveInventoryTypes.Value)
            {
                maxCorpseSlots += byPlayer.InventoryManager.GetOwnInventory(invClassName)?.Count ?? 0;
            }
            return maxCorpseSlots;
        }

        private static ItemStack? TakeSlotContent(ItemSlot slot)
        {
            if (slot.Empty) return null;

            // Skip the player's clothing (not armor)
            if (slot.Inventory.ClassName == GlobalConstants.characterInvClassName)
            {
                bool isArmor = slot.Itemstack.ItemAttributes?["protectionModifiers"].Exists ?? false;
                if (!isArmor)
                {
                    return null;
                }
            }

            return slot.TakeOutWhole();
        }

        public static void CreateDeathPoint(IServerPlayer byPlayer)
        {
            var format = "/waypoint addati {0} ={1} ={2} ={3} {4} {5} Death: {6}";
            var icon = Config.Current.WaypointIcon.Value;
            var pos = byPlayer.Entity.ServerPos.AsBlockPos;
            var isPinned = Config.Current.PinWaypoint.Value;
            var color = Config.Current.WaypointColor.Value;
            var deathTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            string message = string.Format(format, icon, pos.X, pos.Y, pos.Z, isPinned, color, deathTime);
            byPlayer.SendMessageAsClient(message, GlobalConstants.AllChatGroups);
        }

        public static string ClearUID(string uid)
        {
            return Regex.Replace(uid, "[^0-9a-zA-Z]", "");
        }

        public void SaveDeathContent(InventoryGeneric inventory, IPlayer player)
        {
            ICoreAPI api = player.Entity.Api;

            string localPath = Path.Combine("ModData", api.GetWorldId(), Mod.Info.ModID, ClearUID(player.PlayerUID));
            string path = api.GetOrCreateDataPath(localPath);
            string[] files = Directory.GetFiles(path).OrderByDescending(f => new FileInfo(f).Name).ToArray();

            for (int i = files.Length - 1; i > Config.Current.MaxDeathContentSavedPerPlayer.Value - 2; i--)
            {
                File.Delete(files[i]);
            }

            var tree = new TreeAttribute();
            inventory.ToTreeAttributes(tree);

            string name = "inventory-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".dat";
            File.WriteAllBytes(path + "/" + name, tree.ToBytes());
        }

        public InventoryGeneric LoadLastDeathContent(IPlayer player, int offset = 0)
        {
            ICoreAPI api = player.Entity.Api;
            if (Config.Current.MaxDeathContentSavedPerPlayer.Value <= offset)
                throw new IndexOutOfRangeException("offset is too large or save data disabled");

            string localPath = Path.Combine("ModData", api.GetWorldId(), Mod.Info.ModID, ClearUID(player.PlayerUID));
            string path = api.GetOrCreateDataPath(localPath);
            string file = Directory.GetFiles(path)
                .OrderByDescending(f => new FileInfo(f).Name)
                .ToArray()
                .ElementAt(offset);

            var tree = new TreeAttribute();
            tree.FromBytes(File.ReadAllBytes(file));

            var inv = new InventoryGeneric(tree.GetInt("qslots"), "playercorpse-" + player.PlayerUID, api);
            inv.FromTreeAttributes(tree);
            return inv;
        }
    }
}
