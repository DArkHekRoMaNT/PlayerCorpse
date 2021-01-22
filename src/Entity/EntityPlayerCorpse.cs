using System;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace PlayerCorpse
{
    public class EntityPlayerCorpse : EntityAgent
    {
        Random Rand { get { return World.Rand; } }
        public bool IsFlowuped
        {
            get { return AnimManager?.Animator?.GetAnimationState("flowup")?.CurrentFrame == 31; }
        }
        public float AnimShift
        {
            get
            {
                float? frame = AnimManager?.Animator?.GetAnimationState("idle")?.CurrentFrame;
                if (frame == null) return 0;
                return ((float)(frame >= 8f ? frame - 11f : -frame + 5f) + 32f * (IsFlowuped ? 1 : 0)) / 32f + 0.25f;
            }
        }
        public InventoryGeneric inventory;
        public ILoadedSound rotationSound;
        Vec3f soundpos;
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
        SimpleParticleProperties souls = new SimpleParticleProperties(
            1, 1,
            ColorUtil.ToRgba(255, 255, 228, 151),
            new Vec3d(), new Vec3d(),
            new Vec3f(0, 0, 0),
            new Vec3f(0, 0, 0),
            0.25f,
            0.1f,
            0.4f, 0.6f,
            EnumParticleModel.Cube
        );
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
        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            if (rotationSound == null && World.Side == EnumAppSide.Client)
            {
                rotationSound = ((IClientWorldAccessor)World).LoadSound(new SoundParams()
                {
                    Location = new AssetLocation(Constants.MOD_ID, "sounds/creature/soul/buzz.ogg"),
                    ShouldLoop = true,
                    Position = soundpos = Pos.XYZ.ToVec3f().Add(0.5f, 0.25f + AnimShift, 0.5f),
                    DisposeOnFinish = false,
                    Volume = 1f

                });

                rotationSound.Start();
            }

            AnimManager.StartAnimation("flowup");
            AnimManager.StartAnimation("rotating");
            AnimManager.StartAnimation("circle-small");
            AnimManager.StartAnimation("circle-middle");
            AnimManager.StartAnimation("circle-large");
            AnimManager.StartAnimation("idle");
        }
        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
        {
            if (!Config.Current.canFired.Val && damageSource.Type == EnumDamageType.Fire) return false;
            if (!Config.Current.hasHealth.Val) return false;

            return base.ShouldReceiveDamage(damageSource, damage);
        }
        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (soundpos != null)
            {
                soundpos.X = (float)Pos.X;
                soundpos.Y = (float)Pos.Y + AnimShift;
                soundpos.Z = (float)Pos.Z;
                rotationSound.SetPosition(soundpos);
            }

            if (LastInteractPassedMs > 1000) secondsPassed = null;

            if (Api.Side == EnumAppSide.Client && IsFlowuped)
            {
                ICoreClientAPI capi = Api as ICoreClientAPI;
                Vec3d startPos = new Vec3d(SidedPos.X, SidedPos.Y + AnimShift, SidedPos.Z);
                Vec3d endPos = new Vec3d(
                    SidedPos.X + Rand.NextDouble() - Rand.NextDouble(),
                    SidedPos.Y + (Rand.NextDouble() - Rand.NextDouble()) * 0.25f + AnimShift,
                    SidedPos.Z + Rand.NextDouble() - Rand.NextDouble()
                );

                Vec3f minVelo = new Vec3f((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
                minVelo.Normalize();
                minVelo *= 2;


                souls.Color = capi.BlockTextureAtlas.GetRandomColor(
                    capi.BlockTextureAtlas[
                        new AssetLocation("game", "textures/block/glowworm/glowworm.png")
                    ].atlasTextureId
                );

                souls.MinPos = startPos;
                souls.MinVelocity = minVelo;
                souls.WithTerrainCollision = true;

                World.SpawnParticles(souls);
            }
        }
        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            IPlayer byPlayer = World.PlayerByUid((byEntity as EntityPlayer).PlayerUID);
            if (byPlayer == null) return;

            Vec3d startPos = new Vec3d(Pos.X + Rand.NextDouble(), Pos.Y + Rand.NextDouble() * 0.25f, Pos.Z + Rand.NextDouble());
            Vec3d endPos = new Vec3d(byEntity.SidedPos.X, byEntity.SidedPos.Y + byEntity.LocalEyePos.Y - 0.2f, byEntity.SidedPos.Z);

            Vec3f minVelo = new Vec3f((float)(endPos.X - startPos.X), (float)(endPos.Y - startPos.Y), (float)(endPos.Z - startPos.Z));
            minVelo.Normalize();
            minVelo *= 2;

            souls.MinQuantity = 10f;
            souls.AddQuantity = 10f;
            souls.MinPos = startPos;
            souls.MinVelocity = minVelo;
            souls.WithTerrainCollision = true;

            byEntity.World.SpawnParticles(souls, byPlayer);

            if (Api.Side == EnumAppSide.Server)
            {
                if (inventory == null || inventory.Count == 0) Die();
                else if (secondsPassed == null) { secondsPassed = 0; Api.SendMessageAll(secondsPassed.ToString()); }
                else if (secondsPassed > 0)
                {
                    if (byPlayer.PlayerUID != WatchedAttributes.GetString("ownerUID") &&
                        byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) return;

                    foreach (var slot in inventory)
                    {
                        if (slot.Empty) continue;

                        if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                        {
                            Api.World.SpawnItemEntity(slot.Itemstack, byEntity.ServerPos.XYZ);
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
        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            rotationSound?.Stop();
            rotationSound?.Dispose();

            base.OnEntityDespawn(despawn);
        }

        // public override string GetInfoText()
        // {
        //     StringBuilder sb = new StringBuilder();
        //     sb.Append(base.GetInfoText());
        //     sb.AppendLine(
        //         Lang.Get("Owner: ") +
        //         World.PlayerByUid(WatchedAttributes.GetString("ownerUID"))?.PlayerName
        //     );
        //     return sb.ToString();
        // }
        public override string GetName()
        {
            return Lang.Get("{0}'s soul", World.PlayerByUid(WatchedAttributes.GetString("ownerUID"))?.PlayerName);
        }
    }
}