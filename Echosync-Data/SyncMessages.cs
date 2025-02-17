using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echosync_Data.Enums
{
    public enum SyncMessages
    {
        Test,
        Authenticate,
        RequestAuthentication,
        CreateChannel,
        ConnectedPlayersChannel,
        ConnectedPlayersNpc,
        ConnectedPlayersDialogue,
        ConnectedPlayersReady,
        StartNpc,
        EndNpc,
        ClickSuccess,
        Click,
        ClickForce,
        ClickWait,
        ClickWaitCatchup,
        ClickDone,
        ServerShutdown
    }
}
