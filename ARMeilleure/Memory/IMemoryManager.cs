using ARMeilleure.State;
using System;

namespace ARMeilleure.Memory
{
    public interface IMemoryManager : IDisposable
    {
        void Map(long va, long pa, long size);

        void Unmap(long position, long size);

        bool IsMapped(long position);

        long GetPhysicalAddress(long virtualAddress);

        (ulong, ulong)[] GetModifiedRanges(ulong address, ulong size, int id);

        bool IsValidPosition(long position);

        bool AtomicCompareExchangeInt32(long position, int expected, int desired);

        int AtomicIncrementInt32(long position);

        int AtomicDecrementInt32(long position);

        sbyte ReadSByte(long position);

        short ReadInt16(long position);

        int ReadInt32(long position);

        long ReadInt64(long position);

        byte ReadByte(long position);

        ushort ReadUInt16(long position);

        uint ReadUInt32(long position);

        ulong ReadUInt64(long position);

        byte[] ReadBytes(long position, long size);

        void WriteSByte(long position, sbyte value);

        void WriteInt16(long position, short value);

        void WriteInt32(long position, int value);

        void WriteInt64(long position, long value);

        void WriteByte(long position, byte value);

        void WriteUInt16(long position, ushort value);

        void WriteUInt32(long position, uint value);

        void WriteUInt64(long position, ulong value);

        void WriteVector128(long position, V128 value);

        void WriteBytes(long position, byte[] data);
    }
}