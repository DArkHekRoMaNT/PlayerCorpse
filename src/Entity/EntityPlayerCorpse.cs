using System.IO;
using SharedUtils;
using SharedUtils.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    public class EntityPlayerCorpse : EntityAgent
    {
        public Core Core { get; private set; }
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            Core = Api.ModLoader.GetModSystem<Core>();
        }

        // The corpse inventory
        public InventoryGeneric inventory;

        // How many milliseconds have passed since the last interaction check
        private long lastInteractMs;
        public long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - lastInteractMs; }
            set { lastInteractMs = value; }
        }

        // How many seconds have passed since the interaction began
        public float SecondsPassed { get; set; }
        public float requiredSeconds = 3;


        // Called when after the got loaded from the savegame (not called during spawn)
        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (inventory != null)
            {
                foreach (var slot in inventory)
                {
                    slot.Itemstack?.ResolveBlockOrItem(World);
                }
            }
        }



        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
        {
            if (!Config.Current.CanFired.Val && damageSource.Type == EnumDamageType.Fire) return false;
            if (!Config.Current.HasHealth.Val) return false;

            return base.ShouldReceiveDamage(damageSource, damage);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (LastInteractPassedMs > 300)
            {
                SecondsPassed = 0;
                if (Api.Side == EnumAppSide.Client)
                {
                    Core.HudOverlayRenderer.CircleVisible = false;
                }
            }
            else
            {
                SecondsPassed += dt;
                if (Api.Side == EnumAppSide.Client)
                {
                    Core.HudOverlayRenderer.CircleProgress = SecondsPassed / requiredSeconds;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            IPlayer byPlayer = World.PlayerByUid((byEntity as EntityPlayer)?.PlayerUID);
            if (byPlayer == null) return;
            if (byPlayer.PlayerUID != WatchedAttributes.GetString("ownerUID") &&
                byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                //(byPlayer as IServerPlayer)?.SendIngameError("not-corpse-owner");
                (byPlayer as IClientPlayer)?.ShowChatNotification(Lang.Get("game:ingameerror-not-corpse-owner"));
                base.OnInteract(byEntity, itemslot, hitPosition, mode);
                return;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                if (inventory == null || inventory.Count == 0) Die();
                else if (SecondsPassed > requiredSeconds)
                {
                    foreach (var slot in inventory)
                    {
                        if (slot.Empty) continue;

                        if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                        {
                            Api.World.SpawnItemEntity(slot.Itemstack, byEntity.ServerPos.XYZ.AddCopy(0, 1, 0));
                        }
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }
                    Die();
                }
            }

            LastInteractPassedMs = World.ElapsedMilliseconds;
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && inventory != null)
            {
                inventory.Api = Api; // fix strange null
                inventory.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            }

            base.Die(reason, damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Api.Side == EnumAppSide.Client)
            {
                Core.HudOverlayRenderer.CircleVisible = false;
            }
        }

        // Serializes the slots contents to be stored in the SaveGame
        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            if (inventory != null && WatchedAttributes != null)
            {
                WatchedAttributes.SetString("invid", inventory.InventoryID);
                inventory.ToTreeAttributes(WatchedAttributes);
            }
        }

        // Loads the entity from a stored byte array from the SaveGame
        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (WatchedAttributes != null)
            {
                string inventoryID = WatchedAttributes.GetString("invid");

                inventory = new InventoryGeneric(0, inventoryID, Api);
                inventory.FromTreeAttributes(WatchedAttributes);
            }
        }

        // Get the corpse name
        public override string GetName()
        {
            return Lang.Get("{0}'s corpse", WatchedAttributes.GetString("ownerName"));
        }

        // Called when a player looks at the entity with interaction help enabled
        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return new WorldInteraction[] {
                new WorldInteraction{
                    ActionLangCode = ConstantsCore.ModId + ":blockhelp-collect",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}