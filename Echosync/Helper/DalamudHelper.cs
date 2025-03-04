using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace Echosync.Helper
{
    public static class DalamudHelper
    {
        private static IObjectTable? _objectTable;
        private static IClientState? _clientState;
        private static IFramework? _framework;

        public static void Setup(IObjectTable objectTable, IClientState clientState, IFramework framework)
        {
            _objectTable = objectTable;
            _clientState = clientState;
            _framework = framework;
        }

        public static int GetClosePlayers(List<uint> connectedUsers, float maxPlayerDistance)
        {
            var result = 0;
            _framework!.RunOnFrameworkThread(() => { result = GetClosePlayersMainThread(connectedUsers, maxPlayerDistance); });

            return result;
        }
        private static int GetClosePlayersMainThread(List<uint> connectedUsers, float maxPlayerDistance)
        {
            return (from connectedUser
                    in connectedUsers
                    where !SyncClientHelper.ConnectedPlayersNpc.Contains(connectedUser)
                    select _objectTable!.SearchByEntityId(connectedUser))
                        .OfType<IGameObject>()
                            .Select(playerObject => _clientState!.LocalPlayer!.Position - playerObject.Position)
                                .Select(distance => Math.Abs(distance.X) + Math.Abs(distance.Y) + Math.Abs(distance.Z))
                                    .Count(combinedDistance => combinedDistance < maxPlayerDistance);
        }
    }
}