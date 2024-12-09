using CommonLib.UI;
using CommonLib.Utils;
using System;
using System.IO;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PlayerCorpse.Entities
{
    public class EntityPlayerCorpse : EntityAgent
    {
        private ILogger? _modLogger;
        private long _lastInteractMs;
        private HudCircleRenderer _interactRingRenderer = null!;

        private float SecondsPassed { get; set; }

        private long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - _lastInteractMs; }
            set { _lastInteractMs = value; }
        }

        public ILogger ModLogger => _modLogger ?? Api.Logger;
        public InventoryGeneric? Inventory { get; set; }

        public double CreationTime
        {
            get { return WatchedAttributes.GetDouble("creationTime", Api.World.Calendar.TotalHours); }
            set { WatchedAttributes.SetDouble("creationTime", value); }
        }

        public string CreationRealDatetime
        {
            get { return WatchedAttributes.GetString("creationRealDatetime", "no data"); }
            set { WatchedAttributes.SetString("creationRealDatetime", value); }
        }

        public string OwnerUID
        {
            get { return WatchedAttributes.GetString("ownerUID"); }
            set { WatchedAttributes.SetString("ownerUID", value); }
        }

        public string OwnerName
        {
            get { return WatchedAttributes.GetString("ownerName"); }
            set { WatchedAttributes.SetString("ownerName", value); }
        }

        public bool IsFree
        {
            get
            {
                double hoursPassed = Api.World.Calendar.TotalHours - CreationTime;
                int hoursForFree = Core.Config.FreeCorpseAfterTime;

                bool alwaysFree = hoursForFree == 0;
                bool neverFree = hoursForFree < 0;
                bool freeNow = hoursPassed > hoursForFree;

                return alwaysFree || !neverFree && freeNow;
            }
        }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);

            _modLogger = api.ModLoader.GetModSystem<Core>().Mod.Logger;

            if (api is ICoreClientAPI capi)
            {
                _interactRingRenderer = new HudCircleRenderer(capi, new HudCircleSettings
                {
                    Color = 0xFF9500
                });
            }
        }

        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (Inventory != null)
            {
                Inventory.Api = Api;
                Inventory.ResolveBlocksOrItems();
            }
        }

        public override bool ShouldReceiveDamage(DamageSource damageSource, float damage)
        {
            if (Core.Config.CanFired == false && damageSource.Type == EnumDamageType.Fire)
            {
                return false;
            }

            if (Core.Config.HasHealth == false)
            {
                return false;
            }

            return base.ShouldReceiveDamage(damageSource, damage);
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (LastInteractPassedMs > 300)
            {
                if (SecondsPassed != 0 && Api.Side == EnumAppSide.Client)
                {
                    _interactRingRenderer.CircleVisible = false;
                }
                SecondsPassed = 0;
            }
            else
            {
                SecondsPassed += dt;
                if (Api.Side == EnumAppSide.Client)
                {
                    _interactRingRenderer.CircleProgress = SecondsPassed / Core.Config.CorpseCollectionTime;
                    if (SecondsPassed > Core.Config.CorpseCollectionTime)
                    {
                        ForceUpdateSecondsPassedOnServer();
                    }
                }
            }

            if (Api is ICoreClientAPI capi)
            {
                if (OwnerUID == capi.World.Player.PlayerUID && Api.World.Rand.NextDouble() < 0.3)
                {
                    capi.World.SpawnParticles(new SimpleParticleProperties()
                    {
                        MinPos = Pos.XYZ,
                        Color = GetRandomColor(Api.World.Rand),
                        MinSize = 0.2f,
                        MaxSize = 0.3f,
                        MinVelocity = new Vec3f(-0.1f, 0.5f, -0.1f),
                        AddVelocity = new Vec3f(0.2f, 1.5f, 0.2f),
                        MinQuantity = 1,
                        LifeLength = 1,
                        WithTerrainCollision = false,
                        LightEmission = DarkColor.FromARGB(255, 255, 255, 255).RGBA
                    });
                }
            }
        }

        private void ForceUpdateSecondsPassedOnServer()
        {
            if (Api is ICoreClientAPI capi)
            {
                capi.Network.SendEntityPacket(EntityId, 141325, [(byte)SecondsPassed]);
            }
        }

        public override void OnReceivedClientPacket(IServerPlayer player, int packetid, byte[] data)
        {
            base.OnReceivedClientPacket(player, packetid, data);

            if (packetid == 141325)
            {
                if (data?.Length > 0 && data[0] > SecondsPassed)
                {
                    SecondsPassed = data[0];
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            if (byEntity is EntityPlayer entityPlayer)
            {
                IPlayer byPlayer = World.PlayerByUid(entityPlayer.PlayerUID);
                if (byPlayer != null)
                {
                    if (!CanCollect(byPlayer))
                    {
                        if (byPlayer is IServerPlayer sp)
                        {
                            sp.SendIngameError("", Lang.Get("game:ingameerror-not-corpse-owner"));
                        }
                    }
                    else
                    {
                        if (Api.Side == EnumAppSide.Server)
                        {
                            if (Inventory == null || Inventory.Count == 0)
                            {
                                string format = "{0} at {1} is empty and will be removed immediately, id {3}";
                                string msg = string.Format(format, GetName(), SidedPos.XYZ.RelativePos(Api), byPlayer.PlayerName, EntityId);
                                ModLogger.Notification(msg);
                                Die();
                            }
                            else if (SecondsPassed > Core.Config.CorpseCollectionTime)
                            {
                                Collect(byPlayer);
                            }
                        }

                        LastInteractPassedMs = World.ElapsedMilliseconds;
                        return;
                    }
                }
            }

            base.OnInteract(byEntity, itemslot, hitPosition, mode);
        }

        private bool CanCollect(IPlayer byPlayer)
        {
            if (byPlayer.Entity.Alive)
            {
                if (byPlayer.PlayerUID == OwnerUID ||
                    byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ||
                    IsFree)
                {
                    return true;
                }
            }

            return false;
        }

        private void Collect(IPlayer byPlayer)
        {
            if (Inventory != null)
            {
                foreach (var slot in Inventory)
                {
                    if (slot.Empty)
                    {
                        continue;
                    }

                    if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                    {
                        Api.World.SpawnItemEntity(slot.Itemstack, byPlayer.Entity.ServerPos.XYZ.AddCopy(0, 1, 0));
                    }
                    slot.Itemstack = null;
                    slot.MarkDirty();
                }
            }

            string msg = string.Format(
                "{0} at {1} can be collected by {2}, id {3}",
                GetName(),
                SidedPos.XYZ.RelativePos(Api),
                byPlayer.PlayerName,
                EntityId);

            ModLogger.Notification(msg);
            if (Core.Config.DebugMode)
            {
                Api.BroadcastMessage(msg);
            }

            Die();
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource? damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && Inventory != null)
            {
                Inventory.Api = Api; // fix strange null
                Inventory.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            }

            string msg = string.Format(
                "{0} at {1} was destroyed, id {2}",
                GetName(),
                SidedPos.XYZ.RelativePos(Api),
                EntityId);

            ModLogger.Notification(msg);
            if (Core.Config.DebugMode)
            {
                Api.BroadcastMessage(msg);
            }

            base.Die(reason, damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Api.Side == EnumAppSide.Client)
            {
                _interactRingRenderer.CircleVisible = false;
            }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            if (Inventory != null && Inventory.Count > 0 && WatchedAttributes != null)
            {
                WatchedAttributes.SetString("invid", Inventory.InventoryID);
                Inventory.ToTreeAttributes(WatchedAttributes);
            }

            base.ToBytes(writer, forClient);
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (WatchedAttributes != null)
            {
                string inventoryID = WatchedAttributes.GetString("invid");
                int qslots = WatchedAttributes.GetInt("qslots", 0);

                Inventory = new InventoryGeneric(qslots, inventoryID, Api);
                Inventory.FromTreeAttributes(WatchedAttributes);

                if (Api != null)
                {
                    Inventory.ResolveBlocksOrItems();
                }
            }
        }

        public override string GetName()
        {
            return Lang.Get("{0}'s corpse", OwnerName);
        }

        public override string GetInfoText()
        {
            var sb = new StringBuilder();

            sb.Append(base.GetInfoText());
            sb.AppendLine(Lang.Get($"{Constants.ModId}:corpse-created(date={{0}})", CreationRealDatetime));

            if (IsFree)
            {
                sb.AppendLine(Lang.Get($"{Constants.ModId}:corpse-free"));
            }

            return sb.ToString();
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return [new WorldInteraction
            {
                ActionLangCode = $"{Constants.ModId}:blockhelp-collect",
                MouseButton = EnumMouseButton.Right
            }];
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
