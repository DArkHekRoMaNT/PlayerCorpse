using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class EntityBehaviorPlayerCorpseOnDeath : EntityBehavior
    {
        public EntityBehaviorPlayerCorpseOnDeath(Entity entity) : base(entity) { }
        public override string PropertyName() { return "playercorpseondeath"; }
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (entity is EntityPlayer entityPlayer &&
                entityPlayer.Properties.Server?.Attributes?.GetBool("keepContents", false) != true &&
                Config.Current.SaveInventoryTypes.Val.Length != 0)
            {
                ICoreAPI api = entity.Api;
                IPlayer player = api.World.PlayerByUid(entityPlayer.PlayerUID);

                AssetLocation loc = new AssetLocation(Constants.MOD_ID, "playercorpse");
                EntityProperties type = api.World.GetEntityType(loc);
                Entity corpse = api.World.ClassRegistry.CreateEntity(type);

                int quantitySlots = 0;
                foreach (var invClassName in Config.Current.SaveInventoryTypes.Val)
                {
                    quantitySlots += player.InventoryManager.GetOwnInventory(invClassName).Count;
                }

                (corpse as EntityPlayerCorpse).WatchedAttributes.SetString("ownerUID", player.PlayerUID);
                (corpse as EntityPlayerCorpse).inventory = new InventoryGeneric(quantitySlots, "playercorpse-" + player.PlayerUID, api);
                int lastSlotId = 0;
                foreach (var invClassName in Config.Current.SaveInventoryTypes.Val)
                {
                    if (invClassName == GlobalConstants.characterInvClassName &&
                        entityPlayer.Properties.Server?.Attributes?.GetBool("dropArmorOnDeath") != true) continue;

                    IInventory inv = player.InventoryManager.GetOwnInventory(invClassName);
                    foreach (var slot in inv)
                    {
                        if (slot.Empty) continue;
                        if (invClassName == GlobalConstants.characterInvClassName &&
                            slot.Itemstack.ItemAttributes?["protectionModifiers"].Exists != true) continue;

                        (corpse as EntityPlayerCorpse).inventory.TryFlipItems(lastSlotId++, slot);
                        if (!slot.Empty)
                        {
                            try
                            {
                                (corpse as EntityPlayerCorpse).inventory[lastSlotId].Itemstack = slot.Itemstack.Clone();
                                (corpse as EntityPlayerCorpse).inventory[lastSlotId].MarkDirty();

                                slot.Itemstack = null;
                                slot.MarkDirty();
                            }
                            catch (Exception e)
                            {
                                api.Logger.Warning("Player's inventory still contains " + slot.Itemstack.Collectible.Code + ". Trying to force move.");
                                api.Logger.Error(e.Message);
                            }
                        }
                    }
                }

                corpse.ServerPos.SetPos(entityPlayer.ServerPos.XYZ.AddCopy(0, 5, 0));
                corpse.Pos.SetFrom(corpse.ServerPos);
                corpse.World = api.World;

                if (!(corpse as EntityPlayerCorpse).inventory.IsEmpty)
                {
                    if (Config.Current.CreateWaypoint.Val == "always")
                    {
                        string timeString = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        (player as IServerPlayer).SendMessageAsClient(string.Format(
                        "/waypoint addati {0} ={1} ={2} ={3} {4} {5} Death: {6}",
                         Config.Current.WaypointIcon.Val,
                         entity.Pos.X, entity.Pos.Y, entity.Pos.Z,
                         Config.Current.PinWaypoint.Val,
                         Config.Current.WaypointColor.Val,
                         timeString), GlobalConstants.AllChatGroups);
                    }
                    if (Config.Current.CreateDeathSoul.Val)
                        api.World.SpawnEntity(corpse);
                }
            }
            base.OnEntityDeath(damageSourceForDeath);
        }
    }
}