using ChocolArm64.Introspection;
using ChocolArm64.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Intrinsics;

namespace ChocolArm64.Translation
{
    class ILMethodBuilder
    {
        public LocalAlloc LocalAlloc { get; private set; }

        public List<ILInstructionBound> InstructionBounds { get; set; }

        public Stack<ILInstructionBound> InstructionBoundStack { get; set; }

        public ILGenerator Generator { get; private set; }

        private Dictionary<Register, int> _locals;

        private ILBlock[] _ilBlocks;

        private string _subName;

        private int _localsCount;

        public ILMethodBuilder(ILBlock[] ilBlocks, string subName)
        {
            _ilBlocks = ilBlocks;
            _subName  = subName;

            InstructionBounds = new List<ILInstructionBound>();
            InstructionBoundStack = new Stack<ILInstructionBound>();
        }

        public TranslatedSub GetSubroutine()
        {
            LocalAlloc = new LocalAlloc(_ilBlocks, _ilBlocks[0]);

            List<Register> subArgs = new List<Register>();

            void SetArgs(long inputs, RegisterType baseType)
            {
                for (int bit = 0; bit < 64; bit++)
                {
                    long mask = 1L << bit;

                    if ((inputs & mask) != 0)
                    {
                        subArgs.Add(GetRegFromBit(bit, baseType));
                    }
                }
            }

            SetArgs(LocalAlloc.GetIntInputs(_ilBlocks[0]), RegisterType.Int);
            SetArgs(LocalAlloc.GetVecInputs(_ilBlocks[0]), RegisterType.Vector);

            DynamicMethod method = new DynamicMethod(_subName, typeof(long), GetArgumentTypes(subArgs));

            Generator = method.GetILGenerator();

            TranslatedSub subroutine = new TranslatedSub(method, subArgs);

            int argsStart = TranslatedSub.FixedArgTypes.Length;

            _locals = new Dictionary<Register, int>();

            _localsCount = 0;

            for (int index = 0; index < subroutine.SubArgs.Count; index++)
            {
                Register reg = subroutine.SubArgs[index];

                Generator.EmitLdarg(index + argsStart);
                Generator.EmitStloc(GetLocalIndex(reg));
            }

            foreach (ILBlock ilBlock in _ilBlocks)
            {
                ilBlock.Emit(this);
            }

            ProcessInstructionIntrospection();

            return subroutine;
        }

        private Type[] GetArgumentTypes(IList<Register> Params)
        {
            Type[] fixedArgs = TranslatedSub.FixedArgTypes;

            Type[] output = new Type[Params.Count + fixedArgs.Length];

            fixedArgs.CopyTo(output, 0);

            int typeIdx = fixedArgs.Length;

            for (int index = 0; index < Params.Count; index++)
            {
                output[typeIdx++] = GetFieldType(Params[index].Type);
            }

            return output;
        }

        public int GetLocalIndex(Register reg)
        {
            if (!_locals.TryGetValue(reg, out int index))
            {
                Generator.DeclareLocal(GetFieldType(reg.Type));

                index = _localsCount++;

                _locals.Add(reg, index);
            }

            return index;
        }

        private static Type GetFieldType(RegisterType regType)
        {
            switch (regType)
            {
                case RegisterType.Flag:   return typeof(bool);
                case RegisterType.Int:    return typeof(ulong);
                case RegisterType.Vector: return typeof(Vector128<float>);
            }

            throw new ArgumentException(nameof(regType));
        }

        public static Register GetRegFromBit(int bit, RegisterType baseType)
        {
            if (bit < 32)
            {
                return new Register(bit, baseType);
            }
            else if (baseType == RegisterType.Int)
            {
                return new Register(bit & 0x1f, RegisterType.Flag);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(bit));
            }
        }

        public static bool IsRegIndex(int index)
        {
            return (uint)index < 32;
        }

        private void ProcessInstructionIntrospection()
        {
            var il = Generator.GetType()
                              .GetMethod(
                                  "BakeByteArray",
                                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                              ).Invoke(Generator, null) as byte[];

            foreach (var bound in InstructionBounds)
            {
                var opcode = bound.OpCode;
                var start = bound.ILStart;
                var end = bound.ILEnd;

                var instructions = ILReader.ReadInstructions(il, start, end).ToArray();

                if (instructions.Length > 400)
                {
                    Console.WriteLine("Instruction {0} ({1}) emits {2} instructions:", opcode.Instruction.Emitter.Method.Name, opcode.Instruction.Type.Name, instructions.Length);

                    foreach (var instruction in instructions)
                    {
                        Console.WriteLine("\tIL_{0:X4}:  {1}", start, instruction.OpCode.Name);
                        start += instruction.OpCode.Size;
                        start += instruction.OperandSize;
                    }
                }

                ILIntrospectionCounter.Track(opcode, bound.Timer.ElapsedTicks, instructions.Length);
            }

            InstructionBounds.Clear();
            InstructionBoundStack.Clear();
        }
    }
}