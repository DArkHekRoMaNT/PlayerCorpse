using System;
using System.IO;
using SharedUtils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace PlayerCorpse
{
    public class EntityPlayerCorpse : EntityAgent
    {
        Random Rand { get { return World.Rand; } }
        public InventoryGeneric inventory;

        private long lastInteractMs;
        public long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - lastInteractMs; }
            set { lastInteractMs = value; }
        }

        private long startms;
        public float? secondsPassed
        {
            get { return startms == 0 ? null : (World.ElapsedMilliseconds - (long?)startms) / 1000f; }
            set { startms = value == null ? 0 : World.ElapsedMilliseconds + (long)(value * 1000); }
        }

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

            if (LastInteractPassedMs > 300) secondsPassed = null;
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            IPlayer byPlayer = World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
            if (byPlayer == null) return;

            if (Api.Side == EnumAppSide.Server)
            {
                if (inventory == null || inventory.Count == 0) Die();
                else if (secondsPassed == null) { secondsPassed = 0; }
                else if (secondsPassed > 3)
                {
                    if (byPlayer.PlayerUID != WatchedAttributes.GetString("ownerUID") &&
                        byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

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
                else
                {
                    Vec3f vel = byPlayer.Entity.SidedPos.GetViewVector();
                    vel.Negate();
                    vel.Mul((float)secondsPassed * 0.1f + 1);

                    AdvancedParticleProperties props = new AdvancedParticleProperties()
                    {
                        HsvaColor = new NatFloat[] {
                            NatFloat.createUniform(30, 20),
                            NatFloat.createUniform(255, 50),
                            NatFloat.createUniform(255, 50),
                            NatFloat.createUniform(255, 0)
                        },

                        basePos = SidedPos.XYZ,
                        PosOffset = new NatFloat[] {
                            NatFloat.createUniform(-0.5f, 0),
                            NatFloat.createUniform(0, 0),
                            NatFloat.createUniform(-0.1f, 0)
                        },
                        Velocity = new NatFloat[] {
                            NatFloat.createUniform(vel.X, 0.5f),
                            NatFloat.createUniform(vel.Y, 0.1f),
                            NatFloat.createUniform(vel.Z, 0.5f)
                        },
                        GravityEffect = NatFloat.Zero,

                        Quantity = NatFloat.createUniform(10, 5),
                        LifeLength = NatFloat.createUniform(1, 0.9f),
                        Size = NatFloat.createUniform(0.05f, 0.04f),

                        ParticleModel = EnumParticleModel.Quad
                    };

                    Api.World.SpawnParticles(props);
                }
            }

            LastInteractPassedMs = World.ElapsedMilliseconds;
        }
        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death)
                inventory?.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            base.Die(reason, damageSourceForDeath);
        }
        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            if (inventory != null && WatchedAttributes != null)
            {
                WatchedAttributes.SetString("invid", inventory.InventoryID);
                inventory.ToTreeAttributes(WatchedAttributes);
            }
        }
        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (WatchedAttributes != null)
            {
                string inventoryID = WatchedAttributes.GetString("invid");
                int quantitySlots = WatchedAttributes.GetInt("qslots");

                inventory = new InventoryGeneric(quantitySlots, inventoryID, Api);
                inventory.FromTreeAttributes(WatchedAttributes);
            }
        }

        public override string GetName()
        {
            return Lang.Get("{0}'s corpse", World.PlayerByUid(WatchedAttributes.GetString("ownerUID"))?.PlayerName);
        }

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