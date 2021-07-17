using System;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class EntityBehaviorPlayerCorpseOnDeath : EntityBehavior
    {
        public Core Core { get; }

        public EntityBehaviorPlayerCorpseOnDeath(Entity entity) : base(entity)
        {
            Core = entity.Api.ModLoader.GetModSystem<Core>();
        }
        public override string PropertyName() { return "playercorpseondeath"; }
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            if (!(entity is EntityPlayer entityPlayer) ||
                entityPlayer.Properties.Server?.Attributes?.GetBool("keepContents", false) == true)
            {
                base.OnEntityDeath(damageSourceForDeath);
                return;
            }

            ICoreAPI api = entity.Api;
            IPlayer player = api.World.PlayerByUid(entityPlayer.PlayerUID);

            Entity corpse = api.World.ClassRegistry.CreateEntity(
                api.World.GetEntityType(new AssetLocation(ConstantsCore.ModId, "playercorpse"))
            );

            int quantitySlots = 0;
            foreach (var invClassName in Config.Current.SaveInventoryTypes.Val)
            {
                quantitySlots += player.InventoryManager.GetOwnInventory(invClassName).Count;
            }

            if (quantitySlots != 0)
            {
                (corpse as EntityPlayerCorpse).WatchedAttributes.SetString("ownerUID", player.PlayerUID);
                (corpse as EntityPlayerCorpse).inventory = new InventoryGeneric(quantitySlots, "playercorpse-" + player.PlayerUID, api);

                int lastSlotId = 0;
                foreach (var invClassName in Config.Current.SaveInventoryTypes.Val)
                {
                    if (invClassName == GlobalConstants.characterInvClassName &&
                        entityPlayer.Properties.Server?.Attributes?.GetBool("dropArmorOnDeath") != true) continue;

                    foreach (var slot in player.InventoryManager.GetOwnInventory(invClassName))
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
                                api.Logger.Warning("Player's inventory still contains " + slot.Itemstack.Collectible.Code + "");
                                api.Logger.Error(e.Message);
                            }
                        }
                    }
                }

                corpse.ServerPos.SetPos(entityPlayer.ServerPos.XYZ.AddCopy(0, 1, 0));
                corpse.Pos.SetFrom(corpse.ServerPos);
                corpse.World = api.World;

                if (!(corpse as EntityPlayerCorpse).inventory.Empty)
                {
                    if (Config.Current.CreateWaypoint.Val == "always")
                    {
                        (player as IServerPlayer).SendMessageAsClient(string.Format(

                            "/waypoint addati {0} ={1} ={2} ={3} {4} {5} Death: {6}",
                            Config.Current.WaypointIcon.Val,
                            (int)entity.SidedPos.X, (int)entity.SidedPos.Y, (int)entity.SidedPos.Z,
                            Config.Current.PinWaypoint.Val,
                            Config.Current.WaypointColor.Val,
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")

                        ), GlobalConstants.AllChatGroups);
                    }

                    if (Config.Current.CreateCorpse.Val)
                    {
                        api.World.SpawnEntity(corpse);
                    }
                    else
                    {
                        (corpse as EntityPlayerCorpse).inventory.DropAll(corpse.Pos.XYZ);
                    }

                    if (Config.Current.MaxDeathContentSavedPerPlayer.Val > 0)
                    {
                        Core.SaveDeathContent((corpse as EntityPlayerCorpse).inventory, player);
                    }
                }
            }
        }
    }
}