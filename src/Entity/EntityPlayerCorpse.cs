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
        public Core Core { get; private set; }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            Core = Api.ModLoader.GetModSystem<Core>();
        }

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
                int hoursForFree = Config.Current.FreeCorpseAfterTime.Val;

                bool alwaysFree = hoursForFree == 0;
                bool neverFree = hoursForFree < 0;
                bool freeNow = hoursPassed > hoursForFree;

                return alwaysFree || !neverFree && freeNow;
            }
        }


        /// <summary> How many milliseconds have passed since the last interaction check </summary>
        private long lastInteractMs;
        public long LastInteractPassedMs
        {
            get { return World.ElapsedMilliseconds - lastInteractMs; }
            set { lastInteractMs = value; }
        }


        /// <summary> How many seconds have passed since the interaction began </summary>
        public float SecondsPassed { get; set; }


        public override void OnEntityLoaded()
        {
            base.OnEntityLoaded();
            if (Inventory != null)
            {
                foreach (var slot in Inventory)
                {
                    if (slot.Itemstack != null)
                    {
                        slot.Itemstack.ResolveBlockOrItem(World);
                    }
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
                if (SecondsPassed != 0 && Api.Side == EnumAppSide.Client)
                {
                    Core.InteractRingRenderer.CircleVisible = false;
                }
                SecondsPassed = 0;
            }
            else
            {
                SecondsPassed += dt;
                if (Api.Side == EnumAppSide.Client)
                {
                    Core.InteractRingRenderer.CircleProgress = SecondsPassed / Config.Current.CorpseCollectionTime.Val;
                }
            }
        }

        public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode)
        {
            EntityPlayer entityPlayer = byEntity as EntityPlayer;
            if (entityPlayer == null) return;
            IPlayer byPlayer = World.PlayerByUid(entityPlayer.PlayerUID);
            if (byPlayer == null) return;

            // Check owner
            if (byPlayer.PlayerUID != OwnerUID &&
                byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative &&
                !IsFree)
            {
                IServerPlayer sp = byPlayer as IServerPlayer;
                if (sp != null) sp.SendIngameError("", Lang.Get("game:ingameerror-not-corpse-owner"));
                base.OnInteract(byEntity, itemslot, hitPosition, mode);
                return;
            }


            if (Api.Side == EnumAppSide.Server)
            {
                if (Inventory == null || Inventory.Count == 0) Die();
                else if (SecondsPassed > Config.Current.CorpseCollectionTime.Val)
                {
                    foreach (var slot in Inventory)
                    {
                        if (slot.Empty) continue;

                        if (!byPlayer.InventoryManager.TryGiveItemstack(slot.Itemstack))
                        {
                            Api.World.SpawnItemEntity(slot.Itemstack, byEntity.ServerPos.XYZ.AddCopy(0, 1, 0));
                        }
                        slot.Itemstack = null;
                        slot.MarkDirty();
                    }

                    string log = string.Format("{0} at {1} can be collected by {2}, id {3}", GetName(), SidedPos.XYZ.RelativePos(Api), (byEntity as EntityPlayer).Player.PlayerName, EntityId);
                    Core.ModLogger.Notification(log);
                    if (Config.Current.DebugMode.Val) Api.SendMessageAll(log);

                    Die();
                }
            }

            LastInteractPassedMs = World.ElapsedMilliseconds;
        }

        public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
        {
            if (reason == EnumDespawnReason.Death && Inventory != null)
            {
                Inventory.Api = Api; // fix strange null
                Inventory.DropAll(SidedPos.XYZ.AddCopy(0, 1, 0));
            }

            string log = string.Format("{0} at {1} was destroyed, id {2}", GetName(), SidedPos.XYZ.RelativePos(Api), EntityId);
            Core.ModLogger.Notification(log);
            if (Config.Current.DebugMode.Val) Api.SendMessageAll(log);

            base.Die(reason, damageSourceForDeath);
        }

        public override void OnEntityDespawn(EntityDespawnReason despawn)
        {
            base.OnEntityDespawn(despawn);

            if (Api.Side == EnumAppSide.Client)
            {
                Core.InteractRingRenderer.CircleVisible = false;
            }
        }

        public override void ToBytes(BinaryWriter writer, bool forClient)
        {
            base.ToBytes(writer, forClient);

            if (Inventory != null && WatchedAttributes != null)
            {
                WatchedAttributes.SetString("invid", Inventory.InventoryID);
                Inventory.ToTreeAttributes(WatchedAttributes);
            }
        }

        public override void FromBytes(BinaryReader reader, bool forClient)
        {
            base.FromBytes(reader, forClient);

            if (WatchedAttributes != null)
            {
                string inventoryID = WatchedAttributes.GetString("invid");

                Inventory = new InventoryGeneric(0, inventoryID, Api);
                Inventory.FromTreeAttributes(WatchedAttributes);
            }
        }

        /// <summary> Get the corpse name </summary>
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