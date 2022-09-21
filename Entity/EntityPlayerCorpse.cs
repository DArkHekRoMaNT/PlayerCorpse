using CommonLib.Extensions;
using CommonLib.UI;
using CommonLib.Utils;
using System.IO;
using System.Text;
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
        private HudCircleRenderer _ringRenderer;

        public InventoryGeneric Inventory { get; set; }

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
                int hoursForFree = Config.Current.FreeCorpseAfterTime.Value;

                bool alwaysFree = hoursForFree == 0;
                bool neverFree = hoursForFree < 0;
                bool freeNow = hoursPassed > hoursForFree;

                return alwaysFree || !neverFree && freeNow;
            }
        }

        /// <summary> How many milliseconds have passed since the last interaction check </summary>
        long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - lastInteractMs; }
            set { lastInteractMs = value; }
        }
        private long lastInteractMs;


        /// <summary> How many seconds have passed since the interaction began </summary>
        float SecondsPassed { get; set; }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            _ringRenderer = Api.ModLoader.GetModSystem<Core>().InteractRingRenderer;
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
            if (Config.Current.CanFired.Value == false && damageSource.Type == EnumDamageType.Fire)
            {
                return false;
            }

            if (Config.Current.HasHealth.Value == false)
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
                    _ringRenderer.CircleVisible = false;
                }
                SecondsPassed = 0;
            }
            else
            {
                SecondsPassed += dt;
                if (Api.Side == EnumAppSide.Client)
                {
                    _ringRenderer.CircleProgress = SecondsPassed / Config.Current.CorpseCollectionTime.Value;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            if (entityPlayer != null)
            {
                IPlayer byPlayer = World.PlayerByUid(entityPlayer.PlayerUID);
                if (byPlayer != null)
                {
                    if (!CanCollect(byPlayer))
                    {
                        IServerPlayer sp = byPlayer as IServerPlayer;
                        if (sp != null)
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
                                Core.ModLogger.Notification(msg);
                                Die();
                            }
                            else if (SecondsPassed > Config.Current.CorpseCollectionTime.Value)
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
            return byPlayer.PlayerUID == OwnerUID ||
                   byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ||
                   IsFree;
        }

        private void Collect(IPlayer byPlayer)
        {
            foreach (var slot in Inventory)
            {
                if (slot.Empty) continue;

                if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                {
                    Api.World.SpawnItemEntity(slot.Itemstack, byPlayer.Entity.ServerPos.XYZ.AddCopy(0, 1, 0));
                }
                slot.Itemstack = null;
                slot.MarkDirty();
            }

            string format = "{0} at {1} can be collected by {2}, id {3}";
            string msg = string.Format(format, GetName(), SidedPos.XYZ.RelativePos(Api), byPlayer.PlayerName, EntityId);
            Core.ModLogger.Notification(msg);
            if (Config.Current.DebugMode.Value)
            {
                Api.SendMessageToAll(msg);
            }

            Die();
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && Inventory != null)
            {
                Inventory.Api = Api; // fix strange null
                Inventory.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            }

            string format = "{0} at {1} was destroyed, id {2}";
            string msg = string.Format(format, GetName(), SidedPos.XYZ.RelativePos(Api), EntityId);
            Core.ModLogger.Notification(msg);
            if (Config.Current.DebugMode.Value)
            {
                Api.SendMessageToAll(msg);
            }

            base.Die(reason, damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Api.Side == EnumAppSide.Client)
            {
                _ringRenderer.CircleVisible = false;
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
            StringBuilder str = new StringBuilder();

            str.Append(base.GetInfoText());
            str.AppendLine(Lang.Get(Core.ModId + ":corpse-created(date={0})", CreationRealDatetime));

            if (IsFree)
            {
                str.AppendLine(Lang.Get(Core.ModId + ":corpse-free"));
            }

            return str.ToString();
        }

        public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
        {
            return new WorldInteraction[] {
                new WorldInteraction{
                    ActionLangCode = Core.ModId + ":blockhelp-collect",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }
    }
}