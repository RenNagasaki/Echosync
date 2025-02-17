using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echosync.DataClasses
{
    public class LogConfig
    {
        #region General
        public bool ShowGeneralDebugLog { get; set; } = true;
        public bool ShowGeneralErrorLog { get; set; } = true;
        public bool GeneralJumpToBottom { get; set; } = true;
        #endregion
        #region Rest
        public bool ShowSyncDebugLog { get; set; } = true;
        public bool ShowSyncErrorLog { get; set; } = true;
        public bool ShowSyncId0 { get; set; } = true;
        public bool SyncJumpToBottom { get; set; } = true;
        #endregion
    }
}
