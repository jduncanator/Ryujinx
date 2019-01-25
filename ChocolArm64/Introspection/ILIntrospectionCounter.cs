using ChocolArm64.Decoders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ChocolArm64.Introspection
{
    public static class ILIntrospectionCounter
    {
        private const int RollingAverageWindow = 512;

        private static ConcurrentDictionary<ILKey, ulong> m_EmitCount;
        private static ConcurrentDictionary<ILKey, Queue<long>> m_EmitTime;
        private static ConcurrentDictionary<ILKey, List<int>> m_SizeCounter;
        private static ConcurrentDictionary<long, SubroutineExecutionTime> m_JitTime;

        private static Stopwatch m_Timer;

        static ILIntrospectionCounter()
        {
            m_Timer = Stopwatch.StartNew();
            m_EmitTime = new ConcurrentDictionary<ILKey, Queue<long>>();
            m_EmitCount = new ConcurrentDictionary<ILKey, ulong>();
            m_SizeCounter = new ConcurrentDictionary<ILKey, List<int>>();
            m_JitTime = new ConcurrentDictionary<long, SubroutineExecutionTime>();
        }

        internal static void Track(OpCode64 opcode, long emitTicks, int size)
        {
            var key = new ILKey(opcode);

            // Update the emit time for this instruction
            m_EmitTime.AddOrUpdate(key, new Queue<long>(new[] { emitTicks }), (id, queue) => {
                if (queue.Count >= RollingAverageWindow)
                    queue.Dequeue();

                queue.Enqueue(emitTicks);

                return queue;
            });

            // Update the amount of times we've emitted this instruction
            m_EmitCount.AddOrUpdate(key, 1, (id, count) => count + 1);

            // Keep track of the CIL size of the instruction. Depending on the
            // context, the same instruction could have multiple different CIL
            // lengths, so we store them all against the OpCode in a List<T>.
            m_SizeCounter.AddOrUpdate(key, new List<int> { size }, (id, list) => {
                if (!list.Contains(size))
                    list.Add(size);

                return list;
            });
        }

        internal static void TrackSubroutine(
            long subroutine,
            TimeSpan? tier0Time,
            TimeSpan? tier1Time,
            TimeSpan? ryuJitTime,
            TimeSpan? executionTime)
        {
            var set = new SubroutineExecutionTime {
                ExecutionCount = 1,
                ExecutionTime = executionTime,
                RyuJitTime = ryuJitTime,
                Tier0JitTime = tier0Time,
                Tier1JitTime = tier1Time
            };

            // Update the emit time for this instruction
            m_JitTime.AddOrUpdate(subroutine, set, (sub, obj) => {
                if (tier0Time.HasValue && !obj.Tier0JitTime.HasValue)
                    obj.Tier0JitTime = tier0Time.Value;

                if (tier1Time.HasValue && !obj.Tier1JitTime.HasValue)
                    obj.Tier1JitTime = tier1Time.Value;

                if (ryuJitTime.HasValue && !obj.RyuJitTime.HasValue)
                    obj.RyuJitTime = ryuJitTime.Value;

                if (executionTime.HasValue && !obj.ExecutionTime.HasValue)
                    obj.ExecutionTime = executionTime.Value;

                obj.ExecutionCount++;

                return obj;
            });
        }

        internal static void TrackSubroutine(
            long subroutine,
            SubroutineExecutionTime set)
        {
            set.ExecutionCount = 1;

            m_JitTime.AddOrUpdate(subroutine, set, (sub, obj) => {
                if (set.Tier0JitTime.HasValue && !obj.Tier0JitTime.HasValue)
                    obj.Tier0JitTime = set.Tier0JitTime.Value;

                if (set.Tier1JitTime.HasValue && !obj.Tier1JitTime.HasValue)
                    obj.Tier1JitTime = set.Tier1JitTime.Value;

                if (set.RyuJitTime.HasValue && !obj.RyuJitTime.HasValue)
                    obj.RyuJitTime = set.RyuJitTime.Value;

                if (set.ExecutionTime.HasValue && !obj.ExecutionTime.HasValue)
                    obj.ExecutionTime = set.ExecutionTime.Value;

                obj.ExecutionCount++;

                return obj;
            });
        }

        public static void PrintStatistics()
        {
            m_Timer.Stop();

            Console.WriteLine("ARM64 Instruction Information");
            Console.WriteLine("-----------------------------");
            Console.WriteLine("                             ");

            Console.WriteLine("Application run for {0} seconds.", m_Timer.Elapsed.TotalSeconds);
            Console.WriteLine("Encountered {0} ARM64 instructions.", m_EmitCount.Count());

            Console.WriteLine("                             ");

            Console.WriteLine("ARM64 Insts encountered count");
            Console.WriteLine("-----------------------------");


            var emitCount = m_EmitCount.OrderByDescending(x => x.Value);

            foreach (var emit in emitCount)
            {
                var opcode = emit.Key.OpCode.Instruction;

                Console.WriteLine("\t{0} ({1}) encountered {2} times", opcode.Emitter.Method?.Name, opcode.Type?.Name, emit.Value);
            }

            Console.WriteLine("                             ");

            Console.WriteLine("Instructions by CIL length");
            Console.WriteLine("-----------------------------");


            var instSizes = m_SizeCounter.SelectMany(x => x.Value.Select(y => new { OpCode = x.Key.OpCode, Size = y }))
                                         .OrderByDescending(x => x.Size);

            foreach (var instruction in instSizes)
            {
                var opcode = instruction.OpCode;

                Console.WriteLine("\t{0} ({1}) emits {2} instructions", opcode.Emitter.Method?.Name, opcode.Instruction.Type, instruction.Size);
            }

            Console.WriteLine("                             ");

            Console.WriteLine("Instructions by Emit time");
            Console.WriteLine("-----------------------------");


            var instTime = m_EmitTime.Select(x => new { OpCode = x.Key.OpCode, Ticks = x.Value.Average() })
                                     .OrderByDescending(x => x.Ticks);

            foreach (var instruction in instTime)
            {
                var opcode = instruction.OpCode;

                Console.WriteLine("\t{0} ({1}) takes {2:0.000} ticks ({3:0.000} µs) to emit on average",
                    opcode.Emitter.Method?.Name,
                    opcode.Instruction.Type,
                    instruction.Ticks,
                    instruction.Ticks / (Stopwatch.Frequency / 1000 / 1000f));
            }

            Console.WriteLine("                             ");

            Console.WriteLine("Subroutines JIT time");
            Console.WriteLine("-----------------------------");

            var jitTimes = m_JitTime.OrderByDescending(x => x.Value.RyuJitTime)
                                    .Select(x => new { Subroutine = x.Key, ExecutionObject = x.Value });

            foreach (var subTimes in jitTimes)
            {
                Console.WriteLine("\tSub{0} executed {1} times. Took {2:0} ticks ({3:0.000} µs) to emit, {4:0} ticks ({5:0.000} µs) to JIT",
                    subTimes.Subroutine.ToString("X8"),
                    subTimes.ExecutionObject.ExecutionCount,
                    subTimes.ExecutionObject.Tier0JitTime.Value.Ticks,
                    subTimes.ExecutionObject.Tier0JitTime.Value.Ticks / (Stopwatch.Frequency / 1000 / 1000f),
                    subTimes.ExecutionObject.RyuJitTime.Value.Ticks,
                    subTimes.ExecutionObject.RyuJitTime.Value.Ticks / (Stopwatch.Frequency / 1000 / 1000f));
            }
        }
    }

    class ILKey
    {
        public OpCode64 OpCode { get; set; }

        public ILKey(OpCode64 opcode)
        {
            this.OpCode = opcode;
        }

        public override bool Equals(object obj)
        {
            return obj.GetHashCode() == this.GetHashCode();
        }

        public override int GetHashCode()
        {
            var method = OpCode.Instruction.Emitter.Method;
            var type = OpCode.Instruction.Type;

            var methCode = method == null ? "".GetHashCode() : method.Name.GetHashCode();
            var typeCode = type == null ? 0 : type.GetHashCode();

            return methCode ^ typeCode;
        }

        public override string ToString()
        {
            return String.Format("{0} ({1})", OpCode.Instruction.Emitter.Method?.Name, OpCode.Instruction.Type?.Name);
        }
    }
}
