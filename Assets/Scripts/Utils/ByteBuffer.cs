using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace FairyGUI.Utils
{
    /// <summary>
    /// 一个用于读取和写入各种数据类型（如字节数组、整数、浮点数、字符串等）到字节缓冲区的工具类。
    /// </summary>
    public class ByteBuffer
    {
        /// <summary>
        /// 标记缓冲区是否使用小端字节序（默认值为false，表示大端字节序）。
        /// </summary>
        public bool littleEndian;

        /// <summary>
        /// 字符串表，用于通过索引读取和写入字符串。
        /// </summary>
        public string[] stringTable;

        /// <summary>
        /// 数据格式的版本号。
        /// </summary>
        public int version;

        private int _pointer;  // 当前在缓冲区中的位置指针
        private int _offset;   // 字节数组的起始偏移位置
        private int _length;   // 数据的长度
        private byte[] _data;  // 存储数据的字节数组

        // 用于转换操作的临时字节数组（如读取浮动数或双精度数时使用）。
        static byte[] temp = new byte[8];

        /// <summary>
        /// 初始化 ByteBuffer，指定数据、偏移量和可选的长度。
        /// </summary>
        /// <param name="data">要读取的字节数组。</param>
        /// <param name="offset">在字节数组中的起始偏移位置。</param>
        /// <param name="length">要读取的长度，如果为负数，表示使用从偏移量开始的剩余字节。</param>
        public ByteBuffer(byte[] data, int offset = 0, int length = -1)
        {
            _data = data;
            _pointer = 0;
            _offset = offset;
            _length = length < 0 ? data.Length - offset : length;
            littleEndian = false;  // 默认使用大端字节序
        }

        /// <summary>
        /// 获取或设置当前的缓冲区位置。
        /// </summary>
        public int position
        {
            get { return _pointer; }
            set { _pointer = value; }
        }

        /// <summary>
        /// 获取缓冲区的总长度。
        /// </summary>
        public int length
        {
            get { return _length; }
        }

        /// <summary>
        /// 判断是否还有足够的字节可以读取。
        /// </summary>
        public bool bytesAvailable
        {
            get { return _pointer < _length; }
        }

        /// <summary>
        /// 获取或设置缓冲区的字节数组。
        /// </summary>
        public byte[] buffer
        {
            get { return _data; }
            set
            {
                _data = value;
                _pointer = 0;
                _offset = 0;
                _length = _data.Length;
            }
        }

        /// <summary>
        /// 跳过指定数量的字节并返回新的缓冲区位置。
        /// </summary>
        /// <param name="count">要跳过的字节数。</param>
        /// <returns>跳过后的新位置。</returns>
        public int Skip(int count)
        {
            _pointer += count;
            return _pointer;
        }

        /// <summary>
        /// 从缓冲区读取一个字节。
        /// </summary>
        /// <returns>读取的字节。</returns>
        public byte ReadByte()
        {
            return _data[_offset + _pointer++];
        }

        /// <summary>
        /// 从缓冲区读取指定数量的字节并将它们复制到输出数组中。
        /// </summary>
        /// <param name="output">存放读取字节的目标数组。</param>
        /// <param name="destIndex">目标数组中的起始位置。</param>
        /// <param name="count">要读取的字节数。</param>
        /// <returns>包含读取字节的目标数组。</returns>
        public byte[] ReadBytes(byte[] output, int destIndex, int count)
        {
            if (count > _length - _pointer)
                throw new ArgumentOutOfRangeException();

            Array.Copy(_data, _offset + _pointer, output, destIndex, count);
            _pointer += count;
            return output;
        }

        /// <summary>
        /// 从缓冲区读取指定数量的字节并返回一个新的字节数组。
        /// </summary>
        /// <param name="count">要读取的字节数。</param>
        /// <returns>包含读取字节的新数组。</returns>
        public byte[] ReadBytes(int count)
        {
            if (count > _length - _pointer)
                throw new ArgumentOutOfRangeException();

            byte[] result = new byte[count];
            Array.Copy(_data, _offset + _pointer, result, 0, count);
            _pointer += count;
            return result;
        }

        /// <summary>
        /// 从缓冲区读取一个新的缓冲区数据，首先读取4字节表示数据长度。
        /// </summary>
        /// <returns>读取的数据构成的 ByteBuffer 对象。</returns>
        public ByteBuffer ReadBuffer()
        {
            int count = ReadInt();
            ByteBuffer ba = new ByteBuffer(_data, _pointer, count);
            ba.stringTable = stringTable;
            ba.version = version;
            _pointer += count;
            return ba;
        }

        /// <summary>
        /// 从缓冲区读取一个字符（通过读取两个字节转换为短整型）。
        /// </summary>
        /// <returns>读取的字符值。</returns>
        public char ReadChar()
        {
            return (char)ReadShort();
        }

        /// <summary>
        /// 从缓冲区读取一个布尔值（读取字节 1 为 true，0 为 false）。
        /// </summary>
        /// <returns>读取的布尔值。</returns>
        public bool ReadBool()
        {
            bool result = _data[_offset + _pointer] == 1;
            _pointer++;
            return result;
        }

        /// <summary>
        /// 从缓冲区读取一个 2 字节的短整型值。
        /// </summary>
        /// <returns>读取的短整型值。</returns>
        public short ReadShort()
        {
            int startIndex = _offset + _pointer;
            _pointer += 2;
            if (littleEndian)
                return (short)(_data[startIndex] | (_data[startIndex + 1] << 8));
            else
                return (short)((_data[startIndex] << 8) | _data[startIndex + 1]);
        }

        /// <summary>
        /// 从缓冲区读取一个无符号的 2 字节的值（ushort）。
        /// </summary>
        /// <returns>读取的无符号短整型值。</returns>
        public ushort ReadUshort()
        {
            return (ushort)ReadShort();
        }

        /// <summary>
        /// 从缓冲区读取一个 4 字节的整型值。
        /// </summary>
        /// <returns>读取的整型值。</returns>
        public int ReadInt()
        {
            int startIndex = _offset + _pointer;
            _pointer += 4;
            if (littleEndian)
                return (_data[startIndex]) | (_data[startIndex + 1] << 8) | (_data[startIndex + 2] << 16) | (_data[startIndex + 3] << 24);
            else
                return (_data[startIndex] << 24) | (_data[startIndex + 1] << 16) | (_data[startIndex + 2] << 8) | (_data[startIndex + 3]);
        }

        /// <summary>
        /// 从缓冲区读取一个无符号的 4 字节的值（uint）。
        /// </summary>
        /// <returns>读取的无符号整型值。</returns>
        public uint ReadUint()
        {
            return (uint)ReadInt();
        }

        /// <summary>
        /// 从缓冲区读取一个 4 字节的浮点数值。
        /// </summary>
        /// <returns>读取的浮点数值。</returns>
        public float ReadFloat()
        {
            int startIndex = _offset + _pointer;
            _pointer += 4;
            if (littleEndian == BitConverter.IsLittleEndian)
                return BitConverter.ToSingle(_data, startIndex);
            else
            {
                temp[3] = _data[startIndex];
                temp[2] = _data[startIndex + 1];
                temp[1] = _data[startIndex + 2];
                temp[0] = _data[startIndex + 3];
                return BitConverter.ToSingle(temp, 0);
            }
        }

        /// <summary>
        /// 从缓冲区读取一个 8 字节的长整型值。
        /// </summary>
        /// <returns>读取的长整型值。</returns>
        public long ReadLong()
        {
            int startIndex = _offset + _pointer;
            _pointer += 8;
            if (littleEndian)
            {
                int i1 = (_data[startIndex]) | (_data[startIndex + 1] << 8) | (_data[startIndex + 2] << 16) | (_data[startIndex + 3] << 24);
                int i2 = (_data[startIndex + 4]) | (_data[startIndex + 5] << 8) | (_data[startIndex + 6] << 16) | (_data[startIndex + 7] << 24);
                return ((long)i2 << 32) | (uint)i1;
            }
            else
            {
                int i1 = (_data[startIndex + 4]) | (_data[startIndex + 5] << 8) | (_data[startIndex + 6] << 16) | (_data[startIndex + 7] << 24);
                int i2 = (_data[startIndex]) | (_data[startIndex + 1] << 8) | (_data[startIndex + 2] << 16) | (_data[startIndex + 3] << 24);
                return ((long)i2 << 32) | (uint)i1;
            }
        }
    }
}
