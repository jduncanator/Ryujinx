using ChocolArm64.Decoders;
using System.Diagnostics;

namespace ChocolArm64.Introspection
{
    internal class ILInstructionBound
    {
        public OpCode64 OpCode { get; set; }
        public Stopwatch Timer { get; set; }
        public int ILStart { get; set; }
        public int ILEnd { get; set; }
    }
}