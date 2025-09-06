using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace Echosync.Helper
{
    public static class DalamudHelper
    {

        public static int GetClosePlayers(List<uint> connectedUsers, float maxPlayerDistance)
        {
            var result = 0;
            Plugin.Framework!.RunOnFrameworkThread(() => { result = GetClosePlayersMainThread(connectedUsers, maxPlayerDistance); });

            return result;
        }
        private static int GetClosePlayersMainThread(List<uint> connectedUsers, float maxPlayerDistance)
        {
            return (from connectedUser
                    in connectedUsers
                    where !SyncClientHelper.ConnectedPlayersNpc.Contains(connectedUser)
                    select Plugin.ObjectTable!.SearchByEntityId(connectedUser))
                        .OfType<IGameObject>()
                            .Select(playerObject => Plugin.ClientState!.LocalPlayer!.Position - playerObject.Position)
                                .Select(distance => Math.Abs(distance.X) + Math.Abs(distance.Y) + Math.Abs(distance.Z))
                                    .Count(combinedDistance => combinedDistance < maxPlayerDistance);
        }
    }
}