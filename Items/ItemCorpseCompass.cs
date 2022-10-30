using CommonLib.Extensions;
using CommonLib.Utils;
using PlayerCorpse.Entities;
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse.Items
{
    public class ItemCorpseCompass : Item
    {
        public const long SearchCooldown = 5000;
        public const long OffHandSearchCooldown = 10000;
        public const long OffHandParticleEmitCooldown = 250;
        public const int SearchRadius = 3;

        private readonly SimpleParticleProperties _particles = new()
        {
            MinPos = Vec3d.Zero,
            AddPos = new Vec3d(.2, .2, .2),

            MinVelocity = Vec3f.Zero,
            AddVelocity = Vec3f.Zero,
            RandomVelocityChange = true,

            Bounciness = 0.1f,
            GravityEffect = 0,
            WindAffected = false,
            WithTerrainCollision = true,

            MinSize = 0.3f,
            MaxSize = 0.8f,
            MinQuantity = 1,
            AddQuantity = 5,
            LifeLength = 1f,

            VertexFlags = 100 & VertexFlags.GlowLevelBitMask,
            ParticleModel = EnumParticleModel.Quad
        };

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (handling == EnumHandHandling.NotHandled)
            {
                long lastCorpseSearch = slot.Itemstack.TempAttributes.GetLong("lastCorpseSearch", 0);
                if (lastCorpseSearch + SearchCooldown < api.World.ElapsedMilliseconds)
                {
                    UpdateNearestCorpse(byEntity, slot);
                    slot.Itemstack.TempAttributes.SetLong("lastCorpseSearch", api.World.ElapsedMilliseconds);
                    handling = EnumHandHandling.PreventDefault;
                }

                EmitParticles(slot, byEntity);
            }
        }

        public override void OnHeldIdle(ItemSlot slot, EntityAgent byEntity)
        {
            if (byEntity.LeftHandItemSlot == slot)
            {
                long lastCorpseSearch = slot.Itemstack.TempAttributes.GetLong("lastCorpseSearch", 0);
                if (lastCorpseSearch + OffHandSearchCooldown < api.World.ElapsedMilliseconds)
                {
                    UpdateNearestCorpse(byEntity, slot);
                    slot.Itemstack.TempAttributes.SetLong("lastCorpseSearch", api.World.ElapsedMilliseconds);
                }

                long lastEmit = slot.Itemstack.TempAttributes.GetLong("lastEmitParticlesOffHand", 0);
                if (lastEmit + OffHandParticleEmitCooldown < byEntity.World.ElapsedMilliseconds)
                {
                    EmitParticles(slot, byEntity);
                    slot.Itemstack.TempAttributes.SetLong("lastEmitParticlesOffHand", byEntity.World.ElapsedMilliseconds);
                }
            }

            base.OnHeldIdle(slot, byEntity);
        }

        private void EmitParticles(ItemSlot slot, EntityAgent byEntity)
        {
            Vec3i nearestCorpsePos = slot.Itemstack.Attributes.GetVec3i("nearestCorpsePos");
            if (api.Side == EnumAppSide.Client && nearestCorpsePos != null)
            {
                var targetPos = nearestCorpsePos.ToVec3d().Add(.5, 0, .5);
                var startPos = byEntity.SidedPos.AheadCopy(1).XYZ.Add(0, byEntity.LocalEyePos.Y, 0);
                var relativePos = targetPos - startPos;


                _particles.MinVelocity = relativePos.ToVec3f() / (_particles.LifeLength * 3);
                _particles.MinPos = startPos;
                _particles.AddPos = _particles.MinVelocity.ToVec3d() * 0.1;

                _particles.MinSize = GameMath.Clamp(_particles.MinVelocity.Length() * 0.01f, 0.05f, 3f);
                _particles.MaxSize = _particles.MinSize * 2;

                _particles.Color = GetRandomColor(api.World.Rand);
                api.World.SpawnParticles(_particles);
            }
        }

        private void UpdateNearestCorpse(EntityAgent byEntity, ItemSlot slot)
        {
            if (api.Side == EnumAppSide.Server && byEntity is EntityPlayer playerEntity)
            {
                double distance = double.MaxValue;
                EntityPlayerCorpse? nearestCorpse = null;

                string? ownerUID = playerEntity.PlayerUID;
                if (playerEntity.Player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    ownerUID = null; // show all corpses in creative
                }

                foreach (EntityPlayerCorpse corpse in GetCorpsesAround(SearchRadius, byEntity.ServerPos.XYZInt, ownerUID))
                {
                    double currDistance = byEntity.Pos.SquareDistanceTo(corpse.Pos);
                    if (currDistance <= distance)
                    {
                        distance = currDistance;
                        nearestCorpse = corpse;
                    }
                }

                if (nearestCorpse != null)
                {
                    slot.Itemstack.Attributes.SetVec3i("nearestCorpsePos", nearestCorpse.Pos.XYZInt);
                    slot.MarkDirty();

                    string text = nearestCorpse.OwnerName + "'s corpse found at " + nearestCorpse.SidedPos.XYZ;
                    Core.ModLogger.Notification(text);
                    if (Config.Current.DebugMode.Value)
                    {
                        byEntity.SendMessage(text);
                    }
                }
                else
                {
                    slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosX");
                    slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosY");
                    slot.Itemstack.Attributes.RemoveAttribute("nearestCorpsePosZ");
                    slot.MarkDirty();

                    byEntity.SendMessage(Lang.Get(Core.ModId + ":corpsecompass-corpses-not-found"));
                }
            }
        }

        private IEnumerable<EntityPlayerCorpse> GetCorpsesAround(int radius, Vec3i pos, string? playerUID = null)
        {
            foreach (IServerChunk chunk in GetAllChunksAround(radius, pos))
            {
                foreach (var entity in chunk.Entities)
                {
                    if (entity is EntityPlayerCorpse corpseEntity)
                    {
                        if (playerUID == null || corpseEntity.OwnerUID == playerUID)
                        {
                            yield return corpseEntity;
                        }
                    }
                }
            }
        }

        private IEnumerable<IServerChunk> GetAllChunksAround(int radius, Vec3i pos)
        {
            var sapi = (ICoreServerAPI)api;

            int chunkSize = sapi.WorldManager.ChunkSize;
            int chunksInColum = sapi.WorldManager.MapSizeY / chunkSize;

            int chunkX = pos.X / chunkSize;
            int chunkZ = pos.Z / chunkSize;

            for (int i = chunkX - radius; i <= chunkX + radius; i++)
            {
                for (int j = chunkZ - radius; j <= chunkZ + radius; j++)
                {
                    for (int k = 0; k < chunksInColum; k++)
                    {
                        var chunk = sapi.WorldManager.GetChunk(i, k, j);
                        if (chunk != null)
                        {
                            yield return chunk;
                        }
                        else
                        {
                            Core.ModLogger.Warning("Chunk at X={0} Y={1} Z={2} is not loaded", i, k , j);
                        }
                    }
                }
            }
        }

        private static int GetRandomColor(Random rand)
        {
            int a = 255;
            int r = rand.Next(200, 256);
            int g = rand.Next(100, 156);
            int b = rand.Next(0, 56);

            return ColorUtil.ToRgba(a, r, g, b);
        }
    }
}
