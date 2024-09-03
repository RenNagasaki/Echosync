using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using Echosync.DataClasses;
using Echosync.Enums;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Intrinsics.X86;
using System.Net.Security;

namespace Echosync.Helper
{
    public static class RabbitMQHelper
    {
        public static Dictionary<string, bool> ConnectedPlayers = new Dictionary<string, bool>();
        private static ConnectionFactory? Factory;
        private static IConnection Connection;
        private static IModel? Channel;
        private static EventingBasicConsumer Consumer;
        private static Configuration Configuration;
        private static string ActiveChannel = "";
        private static IClientState ClientState;
        private static string EntityId = "";

        public static void Setup(Configuration configuration, IClientState clientState)
        {
            Configuration = configuration;
            ClientState = clientState;
            EntityId = ClientState.LocalPlayer?.EntityId.ToString();
            Factory = new ConnectionFactory { HostName = DataClasses.Constants.RabbitMQConnectionUrl };
            Factory.RequestedConnectionTimeout = new TimeSpan(0, 0, 5);
            Factory.Port = 5672;
            Factory.UserName = "Echosync-User";
            Factory.Password = "1234432112344321";

            if (!string.IsNullOrWhiteSpace(configuration.SyncChannel) && configuration.ConnectAtStart)
                Connect(new EKEventId(0, Enums.TextSource.Sync));
        }

        public static void Test(EKEventId eventId)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Testing connection to server", eventId);
            Connect(eventId);
            CreateMessage(RabbitMQMessages.Test, eventId);
        }

        private static void ConsumerReceived(object? model, BasicDeliverEventArgs ea)
        {
            var bodyString = "";
            var eventId = new EKEventId(0, TextSource.Sync);
            try
            {
                var body = ea.Body.ToArray();
                bodyString = Encoding.UTF8.GetString(body);
                var bodyStringSplit = bodyString.Split('|');
                var message = (RabbitMQMessages)Convert.ToInt32(bodyStringSplit[0]);
                var sendingPlayer = bodyStringSplit[1];
                eventId = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, TextSource.Sync);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Received '{message}' from '{sendingPlayer}'", eventId);

                switch (message)
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

        public static void Connect(EKEventId eventId)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ActiveChannel))
                    Disconnect(eventId);

                if (Connection == null || !Connection.IsOpen)
                    Connection = Factory.CreateConnection();

                ActiveChannel = Configuration.SyncChannel;
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Connecting to channel: {ActiveChannel}", eventId);
                Connection = Factory.CreateConnection();
                Channel = Connection.CreateModel();
                var res = Channel.QueueDeclare(queue: ActiveChannel,
                                        durable: false,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);
                Consumer = new EventingBasicConsumer(Channel);
                Consumer.Received += ConsumerReceived;
                Channel.BasicConsume(queue: ActiveChannel,
                                        autoAck: true,
                                        consumer: Consumer);

                CreateMessage(RabbitMQMessages.Connect, eventId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while connecting: {ex}", eventId);
            }
        }

        public static void Disconnect(EKEventId eventId)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnecting from channel: {ActiveChannel}", eventId);
                CreateMessage(RabbitMQMessages.Disconnect, eventId);
                Consumer.Received -= ConsumerReceived;
                Consumer.OnCancel();
                Channel?.Dispose();
                Connection?.Dispose();
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
                if (Connection == null || !Connection.IsOpen)
                {
                    Connect(eventId);
                }

                var bodyString = $"{((int)message)}|{EntityId}";
                var body = Encoding.UTF8.GetBytes(bodyString);
                Channel.BasicPublish(exchange: string.Empty,
                                        routingKey: ActiveChannel,
                                        basicProperties: null,
                                        body: body);
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
