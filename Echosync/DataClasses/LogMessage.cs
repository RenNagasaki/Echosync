using System;
using System.Numerics;
using Echosync.Enums;

namespace Echosync.DataClasses
{
    public class LogMessage
    {
        public DateTime timeStamp { get; set; }
        public string method { get; set; }
        public string message { get; set; }
        public Vector4 color { get; set; }
        public EKEventId eventId { get; set; }
        public LogType type { get; set; }
    }
}
