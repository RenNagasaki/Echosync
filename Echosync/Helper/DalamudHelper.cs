using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Echosync.DataClasses;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
namespace Echosync.Helper
{
    public static class DalamudHelper
    {
        private static IObjectTable ObjectTable;
        private static IClientState ClientState;
        private static IFramework Framework;

        public static void Setup(IObjectTable objectTable, IClientState clientState, IFramework framework)
        {
            ObjectTable = objectTable;
            ClientState = clientState;
            Framework = framework;
        }

        public static int GetClosePlayers(List<uint> connectedUsers, float maxPlayerDistance)
        {
            var result = 0;
            Framework.RunOnFrameworkThread(() => { result = GetClosePlayersMainThread(connectedUsers, maxPlayerDistance); });

            return result;
        }
        private static int GetClosePlayersMainThread(List<uint> connectedUsers, float maxPlayerDistance)
        {
            var closePlayers = 0;
            foreach (var connectedUser in connectedUsers)
            {
                if (!SyncClientHelper.ConnectedPlayersNpc.Contains(connectedUser))
                {
                    var playerObject = ObjectTable.SearchByEntityId(connectedUser);

                    if (playerObject != null)
                    {
                        var distance = ClientState.LocalPlayer.Position - playerObject.Position;
                        var combinedDistance = Math.Abs(distance.X) + Math.Abs(distance.Y) + Math.Abs(distance.Z);
                        if (combinedDistance < maxPlayerDistance)
                        {
                            closePlayers++;
                        }
                    }
                }
            }

            return closePlayers;
        }
    }
}
