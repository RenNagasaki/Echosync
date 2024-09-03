using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Echosync.Helper
{
    public static class SyncHelper
    {
        public static bool AllReady = false;

        public static void CheckReady(EKEventId eventId)
        {
            var allReady = true;
            foreach (KeyValuePair<string, bool> connectedPlayer in RabbitMQHelper.ConnectedPlayers)
            {
                if (!connectedPlayer.Value)
                {
                    allReady = false;
                    break;
                }
            }

            AllReady = allReady;
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Current dialogue state '{AllReady}'", eventId);
        }

        public static void Reset(EKEventId eventId)
        {
            foreach (string connectedPlayer in RabbitMQHelper.ConnectedPlayers.Keys)
            {
                RabbitMQHelper.ConnectedPlayers[connectedPlayer] = false;
            }

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Resetting after dialogue advancing", eventId);
        }
    }
}
