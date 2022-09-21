using CommonLib.Utils;
using PlayerCorpse.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse.Items
{
    public class ItemCorpseCompass : Item
    {
        public static SimpleParticleProperties Particles => new()
        {
            MinPos = Vec3d.Zero,
            AddPos = new Vec3d(.2, .2, .2),

            MinVelocity = Vec3f.Zero,
            AddVelocity = Vec3f.Zero,
            GravityEffect = 0,
            RandomVelocityChange = true,

            MinQuantity = 5,
            AddQuantity = 5,

            MinSize = 0.3f,
            MaxSize = 0.8f,

            LifeLength = 1f,

            Color = 0xFF8800,
            ParticleModel = EnumParticleModel.Cube,
        };

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            handling = EnumHandHandling.PreventDefault;

            if (api.Side == EnumAppSide.Server)
            {
                var sapi = (api as ICoreServerAPI)!;

                int chunkSize = sapi.WorldManager.ChunkSize;

                int chunkX = (int)byEntity.SidedPos.X / chunkSize;
                int chunkZ = (int)byEntity.SidedPos.Z / chunkSize;

                Vec3d? lastOwnedCorpsePos = null;
                int ownedCorpseCount = 0;

                int chunksInColum = sapi.WorldManager.MapSizeY / chunkSize;
                for (int i = 0; i < chunksInColum; i++)
                {
                    var chunk = sapi.WorldManager.GetChunk(chunkX, i, chunkZ);
                    foreach (var entity in chunk.Entities)
                    {
                        if (entity is EntityPlayerCorpse corpseEntity)
                        {
                            if (corpseEntity.OwnerUID == (byEntity as EntityPlayer)?.PlayerUID)
                            {
                                lastOwnedCorpsePos = entity.SidedPos.XYZ;
                                ownedCorpseCount++;
                            }

                            if (byEntity.Controls.Sneak)
                            {
                                string text = corpseEntity.OwnerName + "'s corpse found at " + entity.SidedPos.XYZ;
                                Core.ModLogger.Notification(text);
                                if (Config.Current.DebugMode.Value)
                                {
                                    byEntity.SendMessage(text);
                                }
                            }
                        }
                    }
                }

                if (ownedCorpseCount > 0)
                {
                    Particles.MinPos = byEntity.SidedPos.XYZ.Add(0, 1, 0);

                    var relativePos = lastOwnedCorpsePos - byEntity.SidedPos.XYZ.Add(0, 1, 0);
                    Particles.MinVelocity = relativePos.ToVec3f() / (Particles.LifeLength * 3);
                    Particles.AddPos = Particles.MinVelocity.ToVec3d() * 0.1;

                    api.World.SpawnParticles(Particles);
                }
                else
                {
                    byEntity.SendMessage(Lang.Get(Code.Domain + ":corpsecompass-corpses-not-found"));
                }
            }
        }
    }
}