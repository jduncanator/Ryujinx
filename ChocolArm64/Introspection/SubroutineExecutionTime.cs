using System;
using System.Collections.Generic;
using System.Text;

namespace ChocolArm64.Introspection
{
    internal class SubroutineExecutionTime
    {
        public TimeSpan? Tier0JitTime   { get; set; }
        public TimeSpan? Tier1JitTime   { get; set; }
        public TimeSpan? RyuJitTime     { get; set; }
        public TimeSpan? ExecutionTime  { get; set; }
        public ulong     ExecutionCount { get; set; }
    }
}
