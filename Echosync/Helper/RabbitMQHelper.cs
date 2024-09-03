using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using Apache.NMS;
using Apache.NMS.Util;
using FFXIVClientStructs.FFXIV.Client.Game.Event;

namespace Echosync.Helper
{
    public static class RabbitMQHelper
    {
        public static Dictionary<string, bool> ConnectedPlayers = new Dictionary<string, bool>();
        private static IConnectionFactory Factory;
        private static IConnection Connection;
        private static ISession Session;
        private static IDestination Destination;
        private static IMessageConsumer Consumer;
        private static IMessageProducer Producer;
        private static Configuration Configuration;
        private static string ActiveChannel = "";
        private static IClientState ClientState;
        private static string EntityId = "";

        public static void Setup(Configuration configuration, IClientState clientState)
        {
            Configuration = configuration;
            ClientState = clientState;
            EntityId = ClientState.LocalPlayer?.EntityId.ToString();
            try
            {
                IConnectionFactory factory = new NMSConnectionFactory(DataClasses.Constants.RabbitMQConnectionUrl);

                if (!string.IsNullOrWhiteSpace(configuration.SyncChannel) && configuration.ConnectAtStart)
                    Connect(new EKEventId(0, Enums.TextSource.Sync));
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while connecting: {ex}", new EKEventId(0, TextSource.Sync));
            }
        }

        public static void Test(EKEventId eventId)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Testing connection to server", eventId);
            Connect(eventId);
            CreateMessage(RabbitMQMessages.Test, eventId);
        }

        public static void Connect(EKEventId eventId)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ActiveChannel))
                    Disconnect(eventId);

                ActiveChannel = Configuration.SyncChannel;
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Connecting to channel: {ActiveChannel}", eventId);

                if (Connection == null || !Connection.IsStarted)
                    Connection = Factory.CreateConnection(DataClasses.Constants.RabbitMQUserName, DataClasses.Constants.RabbitMQPassword);

                Session = Connection.CreateSession(); 
                Destination = SessionUtil.GetDestination(Session, $"queue://{ActiveChannel}");
                Consumer = Session.CreateConsumer(Destination);
                Consumer.Listener += Consumer_Listener;
                Producer = Session.CreateProducer(Destination);
                Producer.DeliveryMode = MsgDeliveryMode.Persistent;

                CreateMessage(RabbitMQMessages.Connect, eventId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while connecting: {ex}", eventId);
            }
        }

        private static void Consumer_Listener(IMessage message)
        {
            var bodyString = "";
            var eventId = new EKEventId(0, TextSource.Sync);
            try
            {
                var textMessage = (ITextMessage)message;
                bodyString = textMessage.Text;
                var bodyStringSplit = bodyString.Split('|');
                var messageEnum = (RabbitMQMessages)Convert.ToInt32(bodyStringSplit[0]);
                var sendingPlayer = bodyStringSplit[1];
                eventId = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, TextSource.Sync);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Received '{message}' from '{sendingPlayer}'", eventId);

                switch (messageEnum)
                {
                    case RabbitMQMessages.Connect:
                        if (!ConnectedPlayers.ContainsKey(sendingPlayer))
                            ConnectedPlayers.Add(sendingPlayer, false);
                        break;
                    case RabbitMQMessages.Disconnect:
                        if (ConnectedPlayers.ContainsKey(sendingPlayer))
                            ConnectedPlayers.Remove(sendingPlayer);

                        if (sendingPlayer == EntityId)
                            ConnectedPlayers.Clear();
                        break;
                    case RabbitMQMessages.Click:
                        if (!ConnectedPlayers.ContainsKey(sendingPlayer))
                            ConnectedPlayers.Add(sendingPlayer, true);
                        else
                            ConnectedPlayers[sendingPlayer] = true;
                        break;
                    case RabbitMQMessages.Test:
                        break;
                }
                SyncHelper.CheckReady(eventId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Received illegal message '{bodyString}' from channel: {ActiveChannel}", eventId);
            }

            LogHelper.End(MethodBase.GetCurrentMethod().Name, eventId);
        }

        public static void Disconnect(EKEventId eventId)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnecting from channel: {ActiveChannel}", eventId);
                CreateMessage(RabbitMQMessages.Disconnect, eventId);
                Consumer.Dispose();
                Producer.Dispose();
                Session.Dispose();
                Connection.Dispose();
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while disconnecting: {ex}", eventId);
            }
        }

        public static void CreateMessage(RabbitMQMessages message, EKEventId eventId)
        {
            try
            {
                if (Connection == null || !Connection.IsStarted)
                {
                    Connect(eventId);
                }

                var bodyString = $"{((int)message)}|{EntityId}";
                var request = Session.CreateTextMessage(bodyString);

                Producer.Send(request);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' to channel: {ActiveChannel}", eventId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", eventId);
            }
        }

        public static void Dispose()
        {
            Disconnect(new EKEventId(0, TextSource.Sync));
        }
    }
}
