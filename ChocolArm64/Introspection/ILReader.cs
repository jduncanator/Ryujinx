using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace ChocolArm64.Introspection
{
    /// <summary>
    /// Quick and dirty CIL disassembler
    /// </summary>
    public static class ILReader
    {
        private static readonly OpCode[] SingleOpCodes;
        private static readonly OpCode[] DoubleOpCodes;

        static ILReader()
        {
            var fields = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static);

            SingleOpCodes = new OpCode[255];
            DoubleOpCodes = new OpCode[255];

            // Auto-Populate the list of OpCodes from internal
            // .NET Reflection fields.
            foreach(var field in fields)
            {
                var code = (OpCode)field.GetValue(null);

                // Skip reserved CIL instructions
                if (code.OpCodeType == OpCodeType.Nternal)
                    continue;

                if (code.Size == 1)
                {
                    SingleOpCodes[code.Value] = code;
                }
                else
                {
                    DoubleOpCodes[code.Value & 0xff] = code;
                }
            }
        }

        public static IEnumerable<OpCodeMeta> ReadInstructions(byte[] instructions)
        {
            using (var stream = new MemoryStream(instructions))
            using (var reader = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    // Read the OpCode
                    var code = ReadOpCode(reader);

                    // Skip its operands
                    var size = SkipOperands(reader, code);

                    // Return it
                    yield return new OpCodeMeta(code, size);
                }
            }
        }

        public static IEnumerable<OpCodeMeta> ReadInstructions(byte[] instructions, int start, int stop)
        {
            using (var stream = new MemoryStream(instructions, start, stop - start))
            using (var reader = new BinaryReader(stream))
            {
                while (stream.Position < stream.Length)
                {
                    // Read the OpCode
                    var code = ReadOpCode(reader);

                    // Skip its operands
                    var size = SkipOperands(reader, code);

                    // Return it
                    yield return new OpCodeMeta(code, size);
                }
            }
        }

        private static OpCode ReadOpCode(BinaryReader reader)
        {
            var instruction = reader.ReadByte();

            if (instruction != 254)
                return SingleOpCodes[instruction];
            else
                return DoubleOpCodes[reader.ReadByte()];
        }

        private static int SkipOperands(BinaryReader reader, OpCode code)
        {
            int operandSize = 0;

            switch (code.OperandType)
            {
                // 0 Bytes
                case OperandType.InlineNone:
                    break;
                // 1 Bytes
                case OperandType.ShortInlineVar:
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                    reader.ReadByte();
                    operandSize += 1;
                    break;
                // 2 Bytes
                case OperandType.InlineVar:
                    reader.ReadUInt16();
                    operandSize += 2;
                    break;
                // 3 Bytes
                // 4 Bytes
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                    reader.ReadInt32();
                    operandSize += 4;
                    break;
                // 8 bytes
                case OperandType.InlineI8:
                    reader.ReadInt64();
                    operandSize += 8;
                    break;
                // Float
                case OperandType.ShortInlineR:
                    reader.ReadSingle();
                    operandSize += 4;
                    break;

                // Double
                case OperandType.InlineR:
                    reader.ReadDouble();
                    operandSize += 8;
                    break;

                // Misc
                case OperandType.InlineSwitch:
                    int length = reader.ReadInt32();
                    for (int i = 0; i < length; i++)
                        reader.ReadInt32();

                    operandSize += 4 + (4 * length);
                    break;

                default:
                    throw new NotSupportedException();
            }

            return operandSize;
        }
    }

    public class OpCodeMeta
    {
        public OpCode OpCode { get; set; }
        public int OperandSize { get; set; }

        public OpCodeMeta(OpCode opCode, int size)
        {
            this.OpCode = opCode;
            this.OperandSize = size;
        }
    }
}
