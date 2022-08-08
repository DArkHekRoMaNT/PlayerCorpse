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

        public override void Start(ICoreAPI api)
        {
            this.api = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.Network
                .RegisterChannel("loggerextensions")
                .RegisterMessageType(typeof(NetworkSendCurrentMessage));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            api.Network
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
        public static void SendMessageAsClient(this IServerPlayer player, string msg, int chatGroup = -1)
        {
            (player.Entity.Api as ICoreServerAPI).Network
                .GetChannel("loggerextensions")
                .SendPacket(new NetworkSendCurrentMessage()
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
            EntityPlayer entityPlayer = playerEntity as EntityPlayer;
            IPlayer player = entityPlayer == null ? null : api.World.PlayerByUid(entityPlayer.PlayerUID);

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
    }
}