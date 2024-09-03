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
        public bool ShowChatDebugLog { get; set; } = true;
        public bool ShowChatErrorLog { get; set; } = true;
        public bool ShowChatId0 { get; set; } = true;
        public bool ChatJumpToBottom { get; set; } = true;
        #endregion
    }
}
