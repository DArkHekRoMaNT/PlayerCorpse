using CommonLib.Extensions;
using CommonLib.Utils;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class EntityBehaviorPlayerCorpseOnDeath : EntityBehavior
    {
        public override string PropertyName() { return "playercorpseondeath"; }

        public EntityBehaviorPlayerCorpseOnDeath(Entity entity) : base(entity) { }

        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            // Not a player or enabled save inventory after death
            EntityPlayer entityPlayer = entity as EntityPlayer;
            if (entityPlayer == null ||
                (entityPlayer.Properties.Server == null &&
                entityPlayer.Properties.Server.Attributes == null &&
                entityPlayer.Properties.Server.Attributes.GetBool("keepContents", false)))
            {
                base.OnEntityDeath(damageSourceForDeath);
                return;
            }


            ICoreAPI api = entity.Api;
            IPlayer player = api.World.PlayerByUid(entityPlayer.PlayerUID);

            // Calculate the maximum size of inventory for a corpse, depending on the config
            int corpseInvQuantitySlots = 0;
            foreach (var invClassName in Config.Current.SaveInventoryTypes.Value)
            {
                corpseInvQuantitySlots += player.InventoryManager.GetOwnInventory(invClassName).Count;
            }


            if (corpseInvQuantitySlots != 0)
            {
                var corpseEntity = api.World.ClassRegistry.CreateEntity(
                    api.World.GetEntityType(new AssetLocation(Core.ModId, "playercorpse"))
                ) as EntityPlayerCorpse;

                corpseEntity.OwnerUID = player.PlayerUID;
                corpseEntity.OwnerName = player.PlayerName;
                corpseEntity.CreationTime = api.World.Calendar.TotalHours;
                corpseEntity.CreationRealDatetime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                corpseEntity.Inventory = new InventoryGeneric(corpseInvQuantitySlots, "playercorpse-" + player.PlayerUID, api);

                int lastSlotId = 0;
                foreach (var invClassName in Config.Current.SaveInventoryTypes.Value)
                {
                    // Skip armor if it does not drop after death
                    if (invClassName == GlobalConstants.characterInvClassName &&
                        !(entityPlayer.Properties.Server == null &&
                        entityPlayer.Properties.Server.Attributes == null &&
                        entityPlayer.Properties.Server.Attributes.GetBool("dropArmorOnDeath")))
                    {
                        continue;
                    }


                    foreach (var slot in player.InventoryManager.GetOwnInventory(invClassName))
                    {
                        if (slot.Empty) continue;

                        // Skip the player's clothing (not armor)
                        if (invClassName == GlobalConstants.characterInvClassName &&
                            slot.Itemstack.ItemAttributes != null &&
                            !slot.Itemstack.ItemAttributes["protectionModifiers"].Exists)
                        {
                            continue;
                        }

                        // Try to move the stack to the corpse's inventory
                        corpseEntity.Inventory.TryFlipItems(lastSlotId++, slot);

                        // If it didn't work, try forcibly moving stack
                        if (!slot.Empty)
                        {
                            try
                            {
                                corpseEntity.Inventory[lastSlotId].Itemstack = slot.Itemstack.Clone();
                                corpseEntity.Inventory[lastSlotId].MarkDirty();

                                slot.Itemstack = null;
                                slot.MarkDirty();
                            }
                            catch (Exception e)
                            {
                                Core.ModLogger.Warning("Player's inventory still contains " + slot.Itemstack.Collectible.Code + "");
                                Core.ModLogger.Error(e.Message);
                            }
                        }
                    }
                }

                // Attempt to align the corpse to the center of the block so that it does not crawl higher
                corpseEntity.ServerPos.SetPos(new Vec3d(
                    (int)entityPlayer.ServerPos.X + 0.5f,
                    (int)entityPlayer.ServerPos.Y + 0f, // changed from 1 to 0 for fix dancing corpse issue
                    (int)entityPlayer.ServerPos.Z + 0.5f
                ));
                corpseEntity.Pos.SetFrom(corpseEntity.ServerPos);
                corpseEntity.World = api.World;


                // Don't create waypoints, corpse and don't save contents for empty corpse
                if (!corpseEntity.Inventory.Empty)
                {
                    // Create waypoint
                    if (Config.Current.CreateWaypoint.Value == "always")
                    {
                        (player as IServerPlayer).SendMessageAsClient(string.Format(

                            "/waypoint addati {0} ={1} ={2} ={3} {4} {5} Death: {6}",
                            Config.Current.WaypointIcon.Value,
                            (int)entity.SidedPos.X, (int)entity.SidedPos.Y, (int)entity.SidedPos.Z,
                            Config.Current.PinWaypoint.Value,
                            Config.Current.WaypointColor.Value,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

                        ), GlobalConstants.AllChatGroups);
                    }

                    // Create corpse
                    if (Config.Current.CreateCorpse.Value)
                    {
                        api.World.SpawnEntity(corpseEntity);

                        string log = string.Format("Created {0} at {1}, id {2}",
                                                   corpseEntity.GetName(),
                                                   corpseEntity.SidedPos.XYZ.RelativePos(api),
                                                   corpseEntity.EntityId);

                        Core.ModLogger.Notification(log);
                        if (Config.Current.DebugMode.Value) api.SendMessageToAll(log);
                    }
                    else
                    {
                        // Or drop all if corpse creations is disabled
                        corpseEntity.Inventory.DropAll(corpseEntity.Pos.XYZ);
                    }

                    // Save content for /returnthings
                    if (Config.Current.MaxDeathContentSavedPerPlayer.Value > 0)
                    {
                        var dcm = api.ModLoader.GetModSystem<DeathContentManager>();
                        dcm.SaveDeathContent(corpseEntity.Inventory, player);
                    }
                }
            }
        }
    }
}