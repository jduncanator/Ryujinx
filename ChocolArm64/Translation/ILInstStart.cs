using ChocolArm64.Decoders;

namespace ChocolArm64.Translation
{
    struct ILInstStart : IILEmit
    {
        public int OpCodeIndex { get; private set; }
        public OpCode64 OpCode { get; private set; }

        public ILInstStart(int opCodeIndex, OpCode64 opCode)
        {
            this.OpCodeIndex = opCodeIndex;
            this.OpCode = opCode;
        }

        public void Emit(ILMethodBuilder Context) { }
    }
}