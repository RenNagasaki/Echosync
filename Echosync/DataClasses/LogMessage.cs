using System;
using System.Numerics;
using Echosync.Enums;

namespace Echosync.DataClasses
{
    public class LogMessage
    {
        public required DateTime TimeStamp { get; init; }
        public required string Method { get; init; }
        public required string Message { get; init; }
        public required Vector4 Color { get; init; }
        public required EKEventId EventId { get; init; }
        public required LogType Type { get; init; }
    }
}
