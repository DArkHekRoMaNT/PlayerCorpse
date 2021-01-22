using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace PlayerCorpse
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class NetworkSendCurrentMessage
    {
        public string msg;
        public int chatGroup;
        public string playerUID;
    }

    public class LoggerNetwork : ModSystem
    {
        ICoreAPI api;

        static IServerNetworkChannel serverChannel;
        public static IServerNetworkChannel ServerChannel { get { return serverChannel; } }

        IClientNetworkChannel clientChannel;

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            serverChannel = api.Network
                .RegisterChannel("loggerextensions")
                .RegisterMessageType(typeof(NetworkSendCurrentMessage));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            clientChannel = api.Network
                .RegisterChannel("loggerextensions")
                .RegisterMessageType(typeof(NetworkSendCurrentMessage))
                .SetMessageHandler<NetworkSendCurrentMessage>(SendMessageFromServer);
        }

        private void SendMessageFromServer(NetworkSendCurrentMessage netmsg)
        {
            IPlayer player = api.World.PlayerByUid(netmsg.playerUID);
            (player as IClientPlayer).SendMessage(netmsg.msg, netmsg.chatGroup);
        }
    }

    public static class LoggerExtensions
    {
        #region mod-logger

        static string modPrefix = $"[{Constants.MOD_ID}] ";

        public static void ModLog(this ILogger logger, EnumLogType logType, string message, params object[] args)
        {
            logger.Log(logType, modPrefix + message, args);
        }
        public static void ModBuild(this ILogger logger, string message, params object[] args)
        {
            logger.Build(modPrefix + message, args);
        }
        public static void ModChat(this ILogger logger, string message, params object[] args)
        {
            logger.Chat(modPrefix + message, args);
        }
        public static void ModVerboseDebug(this ILogger logger, string message, params object[] args)
        {
            logger.VerboseDebug(modPrefix + message, args);
        }
        public static void ModDebug(this ILogger logger, string message, params object[] args)
        {
            logger.Debug(modPrefix + message, args);
        }
        public static void ModNotification(this ILogger logger, string message, params object[] args)
        {
            logger.Notification(modPrefix + message, args);
        }
        public static void ModWarning(this ILogger logger, string message, params object[] args)
        {
            logger.Warning(modPrefix + message, args);
        }
        public static void ModError(this ILogger logger, string message, params object[] args)
        {
            logger.Error(modPrefix + message, args);
        }
        public static void ModFatal(this ILogger logger, string message, params object[] args)
        {
            logger.Fatal(modPrefix + message, args);
        }
        public static void ModEvent(this ILogger logger, string message, params object[] args)
        {
            logger.Event(modPrefix + message, args);
        }
        public static void ModAudit(this ILogger logger, string message, params object[] args)
        {
            logger.Audit(modPrefix + message, args);
        }
        public static void ModStoryEvent(this ILogger logger, string message, params object[] args)
        {
            logger.StoryEvent(modPrefix + message, args);
        }

        #endregion

        #region sendmessage

        public static void SendMessageAsClient(this IServerPlayer player, string msg, int chatGroup = -1)
        {
            LoggerNetwork.ServerChannel.SendPacket(new NetworkSendCurrentMessage()
            {
                msg = msg,
                chatGroup = chatGroup,
                playerUID = player.PlayerUID
            }, player);
        }

        public static void SendMessage(this IServerPlayer player, string msg, int chatGroup = -1)
        {
            if (chatGroup == -1) chatGroup = GlobalConstants.CurrentChatGroup;
            ICoreAPI api = player.Entity.Api;
            player.SendMessage(chatGroup, msg, EnumChatType.Notification);
            api.World.Logger.Chat(msg);
        }

        public static void SendMessage(this IClientPlayer player, string msg, int chatGroup = -1)
        {
            if (chatGroup == -1) chatGroup = GlobalConstants.CurrentChatGroup;
            ICoreAPI api = player.Entity.Api;
            (api as ICoreClientAPI).SendChatMessage(msg, chatGroup);
            api.World.Logger.Chat(msg);

        }

        public static void SendMessage(this IPlayer player, string msg, int chatGroup = -1)
        {
            ICoreAPI api = player.Entity.Api;
            if (api.Side == EnumAppSide.Server) (player as IServerPlayer).SendMessage(msg, chatGroup);
            else (player as IClientPlayer).SendMessage(msg, chatGroup);
        }

        public static void SendMessage(this Entity playerEntity, string msg, int chatGroup = -1)
        {
            ICoreAPI api = playerEntity.Api;
            IPlayer player = api.World.PlayerByUid((playerEntity as EntityPlayer)?.PlayerUID);

            if (player != null) player.SendMessage(msg, chatGroup);
            else api.World.Logger.Chat(playerEntity.GetName() + " trying say: " + msg);

        }

        public static void SendMessageAll(this ICoreAPI api, string msg, int chatGroup = -1)
        {
            IPlayer[] players = api.World.AllPlayers;
            foreach (IPlayer player in players)
            {
                player.SendMessage(msg, chatGroup);
            }
        }

        #endregion
    }
}