﻿using System;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Text;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Web;
using System.IO;
using System.Drawing;

namespace IPA.DN.CoreUtil.Basic
{
    // FIFO
    public class Fifo
    {
        byte[] p;
        int pos, size;
        public int Size
        {
            get { return size; }
        }
        public byte[] Data
        {
            get
            {
                return this.p;
            }
        }
        public int DataOffset
        {
            get
            {
                return this.pos;
            }
        }
        public int PhysicalSize
        {
            get
            {
                return p.Length;
            }
        }

        int reallocMemSize;
        public const int FifoInitMemSize = 4096;
        public const int FifoReallocMemSize = 65536;
        public const int FifoReallocMemSizeSmall = 65536;

        long totalWriteSize = 0, totalReadSize = 0;

        public long TotalReadSize
        {
            get { return totalReadSize; }
        }
        public long TotalWriteSize
        {
            get { return totalWriteSize; }
        }

        public Fifo()
        {
            init(0);
        }
        public Fifo(int reallocMemSize)
        {
            init(reallocMemSize);
        }

        void init(int reallocMemSize)
        {
            if (reallocMemSize == 0)
            {
                reallocMemSize = FifoReallocMemSize;
            }

            this.size = this.pos = 0;
            this.reallocMemSize = reallocMemSize;

            this.p = new byte[FifoInitMemSize];
        }

        public void Write(Buf buf)
        {
            Write(buf.ByteData);
        }
        public void Write(byte[] src)
        {
            Write(src, src.Length);
        }
        public void SkipWrite(int size)
        {
            Write(null, size);
        }
        public void Write(byte[] src, int size)
        {
            Write(src, 0, size);
        }
        public void Write(byte[] src, int offset, int size)
        {
            int i, need_size;
            bool realloc_flag;

            i = this.size;
            this.size += size;
            need_size = this.pos + this.size;
            realloc_flag = false;

            int memsize = p.Length;
            while (need_size > memsize)
            {
                memsize = Math.Max(memsize, FifoInitMemSize) * 3;
                realloc_flag = true;
            }

            if (realloc_flag)
            {
                byte[] new_p = new byte[memsize];
                Util.CopyByte(new_p, 0, this.p, 0, this.p.Length);
                this.p = new_p;
            }

            if (src != null)
            {
                Util.CopyByte(this.p, this.pos + i, src, offset, size);
            }

            totalWriteSize += size;
        }

        public byte[] Read()
        {
            return Read(this.Size);
        }
        public void ReadToBuf(Buf buf, int size)
        {
            byte[] data = Read(size);

            buf.Write(data);
        }
        public Buf ReadToBuf(int size)
        {
            byte[] data = Read(size);

            return new Buf(data);
        }
        public byte[] Read(int size)
        {
            byte[] ret = new byte[size];
            int read_size = Read(ret);
            Array.Resize<byte>(ref ret, read_size);

            return ret;
        }
        public int Read(byte[] dst)
        {
            return Read(dst, dst.Length);
        }
        public int SkipRead(int size)
        {
            return Read(null, size);
        }
        public int Read(byte[] dst, int size)
        {
            return Read(dst, 0, size);
        }
        public int Read(byte[] dst, int offset, int size)
        {
            int read_size;

            read_size = Math.Min(size, this.size);
            if (read_size == 0)
            {
                return 0;
            }
            if (dst != null)
            {
                Util.CopyByte(dst, offset, this.p, this.pos, size);
            }
            this.pos += read_size;
            this.size -= read_size;

            if (this.size == 0)
            {
                this.pos = 0;
            }

            // メモリの詰め直し
            if (this.pos >= FifoInitMemSize &&
                this.p.Length >= this.reallocMemSize &&
                (this.p.Length / 2) > this.size)
            {
                byte[] new_p;
                int new_size;

                new_size = Math.Max(this.p.Length / 2, FifoInitMemSize);
                new_p = new byte[new_size];
                Util.CopyByte(new_p, 0, this.p, this.pos, this.size);

                this.p = new_p;

                this.pos = 0;
            }

            totalReadSize += read_size;

            return read_size;
        }
    }

    // バッファ
    public class Buf
    {
        MemoryStream buf;

        // コンストラクタ
        public Buf()
        {
            init(new byte[0]);
        }
        public Buf(byte[] data)
        {
            init(data);
        }
        void init(byte[] data)
        {
            buf = new MemoryStream();
            Write(data);
            SeekToBegin();
        }

        // クリア
        public void Clear()
        {
            buf.SetLength(0);
        }

        // 書き込み
        public void WriteByte(byte data)
        {
            buf.WriteByte(data);
        }
        public void Write(byte[] data)
        {
            buf.Write(data, 0, data.Length);
        }
        public void Write(byte[] data, int pos, int len)
        {
            buf.Write(data, pos, len);
        }

        // サイズの取得
        public uint Size
        {
            get
            {
                return (uint)buf.Length;
            }
        }

        // 現在位置の取得
        public uint Pos
        {
            get
            {
                return (uint)buf.Position;
            }
        }

        // バイト列の取得
        public byte[] ByteData
        {
            get
            {
                return buf.ToArray();
            }
        }

        // インデクサによる編集
        public byte this[uint i]
        {
            get
            {
                return buf.GetBuffer()[i];
            }

            set
            {
                buf.GetBuffer()[i] = value;
            }
        }

        // 読み込み
        public byte[] Read()
        {
            return Read(this.Size);
        }
        public byte[] Read(uint size)
        {
            byte[] tmp = new byte[size];
            int i = buf.Read(tmp, 0, (int)size);

            byte[] ret = new byte[i];
            Array.Copy(tmp, 0, ret, 0, i);

            return ret;
        }
        public byte ReadByte()
        {
            byte[] a = Read(1);

            return a[0];
        }

        // シーク
        public void SeekToBegin()
        {
            Seek(0);
        }
        public void SeekToEnd()
        {
            Seek(0, SeekOrigin.End);
        }
        public void Seek(uint offset)
        {
            Seek(offset, SeekOrigin.Begin);
        }
        public void Seek(uint offset, SeekOrigin mode)
        {
            buf.Seek(offset, mode);
        }

        // short 型の読み出し
        public ushort ReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt16(data, 0);
        }
        public ushort RawReadShort()
        {
            byte[] data = Read(2);
            if (data.Length != 2)
            {
                return 0;
            }
            return BitConverter.ToUInt16(data, 0);
        }

        // int 型の読み出し
        public uint ReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt32(data, 0);
        }
        public uint RawReadInt()
        {
            byte[] data = Read(4);
            if (data.Length != 4)
            {
                return 0;
            }
            return BitConverter.ToUInt32(data, 0);
        }

        // int64 型の読み出し
        public ulong ReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            return BitConverter.ToUInt64(data, 0);
        }
        public ulong RawReadInt64()
        {
            byte[] data = Read(8);
            if (data.Length != 8)
            {
                return 0;
            }
            return BitConverter.ToUInt64(data, 0);
        }

        // ANSI 文字列の読み出し
        public string ReadStr()
        {
            return ReadStr(false);
        }
        public string ReadStr(bool include_null)
        {
            uint len = ReadInt();
            byte[] data = Read(len - (uint)(include_null ? 1 : 0));
            return Str.ShiftJisEncoding.GetString(data);
        }

        public string ReadAsciiStr()
        {
            return ReadAsciiStr(false);
        }
        public string ReadAsciiStr(bool include_null)
        {
            uint len = ReadInt();
            byte[] data = Read(len - (uint)(include_null ? 1 : 0));
            return Str.AsciiEncoding.GetString(data);
        }

        // Unicode 文字列の読み出し
        public string ReadUniStr()
        {
            return ReadUniStr(false);
        }
        public string ReadUniStr(bool include_null)
        {
            uint len = ReadInt();
            if (len == 0)
            {
                return null;
            }
            byte[] data = Read(len - (uint)(include_null ? 2 : 0));
            return Str.Utf8Encoding.GetString(data);
        }

        // short 型の書き込み
        public void WriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteShort(ushort shortValue)
        {
            byte[] data = BitConverter.GetBytes(shortValue);
            Write(data);
        }

        // int 型の書き込み
        public void WriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt(uint intValue)
        {
            byte[] data = BitConverter.GetBytes(intValue);
            Write(data);
        }

        // int64 型の書き込み
        public void WriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            if (Env.IsLittleEndian)
            {
                Array.Reverse(data);
            }
            Write(data);
        }
        public void RawWriteInt64(ulong int64Value)
        {
            byte[] data = BitConverter.GetBytes(int64Value);
            Write(data);
        }

        // 1 行を文字列として読み込み
        public string ReadNextLineAsString()
        {
            return ReadNextLineAsString(Str.Utf8Encoding);
        }
        public string ReadNextLineAsString(Encoding encoding)
        {
            byte[] ret = ReadNextLineAsData();
            if (ret == null)
            {
                return null;
            }

            return encoding.GetString(ret);
        }

        // 1 行をバイト列として読み込み
        public byte[] ReadNextLineAsData()
        {
            if (this.Size <= this.Pos)
            {
                // 最後まで読んだ
                return null;
            }

            // 現在位置を記憶
            long pos = buf.Position;

            // 13 または 10 を探索
            long i;
            byte[] tmp = new byte[1];
            for (i = pos; i < buf.Length; i++)
            {
                buf.Read(tmp, 0, 1);

                if (tmp[0] == 13 || tmp[0] == 10)
                {
                    if (tmp[0] == 13)
                    {
                        if ((i + 2) < buf.Length)
                        {
                            i++;
                        }
                    }

                    break;
                }
            }

            long len = i - pos;

            buf.Position = pos;

            // len だけ読み込む
            byte[] ret = Read((uint)((int)len));

            // シークする
            try
            {
                Seek(1, SeekOrigin.Current);
            }
            catch
            {
            }

            if (ret.Length >= 1 && ret[ret.Length - 1] == 13)
            {
                Array.Resize<byte>(ref ret, ret.Length - 1);
            }

            return ret;
        }

        // ANSI 文字列の書き込み
        public void WriteStr(string strValue)
        {
            WriteStr(strValue, false);
        }
        public void WriteStr(string strValue, bool include_null)
        {
            byte[] data = Str.ShiftJisEncoding.GetBytes(strValue);
            WriteInt((uint)data.Length + (uint)(include_null ? 1 : 0));
            Write(data);
        }
        public void WriteAsciiStr(string strValue)
        {
            WriteAsciiStr(strValue, false);
        }
        public void WriteAsciiStr(string strValue, bool include_null)
        {
            byte[] data = Str.AsciiEncoding.GetBytes(strValue);
            WriteInt((uint)data.Length + (uint)(include_null ? 1 : 0));
            Write(data);
        }

        // Unicode 文字列の書き込み
        public void WriteUniStr(string strValue)
        {
            WriteUniStr(strValue, false);
        }
        public void WriteUniStr(string strValue, bool include_null)
        {
            byte[] data = Str.Utf8Encoding.GetBytes(strValue);
            WriteInt((uint)data.Length + (uint)(include_null ? 2 : 0));
            Write(data);
        }

        // ファイルから読み込み
        public static Buf ReadFromFile(string filename)
        {
            return new Buf(IO.ReadFile(filename));
        }

        // バッファから読み込み (ハッシュを調べる)
        public static Buf ReadFromBufWithHash(Buf buf)
        {
            byte[] filedata = buf.ByteData;
            if (filedata.Length < 20)
            {
                throw new ApplicationException("filedata.Length < 20");
            }
            byte[] hash = Util.CopyByte(filedata, 0, 20);
            byte[] data = Util.CopyByte(filedata, 20);
            byte[] hash2 = Secure.HashSHA1(data);

            if (Util.CompareByte(hash, hash2) == false)
            {
                throw new ApplicationException("hash mismatch");
            }

            return new Buf(data);
        }

        // ファイルから読み込み (ハッシュ調べる)
        public static Buf ReadFromFileWithHash(string filename)
        {
            byte[] filedata = IO.ReadFile(filename);
            if (filedata.Length < 20)
            {
                throw new ApplicationException("filedata.Length < 20");
            }
            byte[] hash = Util.CopyByte(filedata, 0, 20);
            byte[] data = Util.CopyByte(filedata, 20);
            byte[] hash2 = Secure.HashSHA1(data);

            if (Util.CompareByte(hash, hash2) == false)
            {
                throw new ApplicationException("hash mismatch");
            }

            return new Buf(data);
        }

        // ファイルに書き込み
        public void WriteToFile(string filename)
        {
            IO.SaveFile(filename, this.ByteData);
        }

        // ファイルに書き込み (ハッシュ付ける)
        public void WriteToFileWithHash(string filename)
        {
            byte[] data = this.ByteData;
            byte[] hash = Secure.HashSHA1(data);

            Buf b = new Buf();
            b.Write(hash);
            b.Write(data);
            b.WriteToFile(filename);
        }

        // ストリームから読み込み
        public static Buf ReadFromStream(Stream st)
        {
            Buf ret = new Buf();
            int size = 32767;

            while (true)
            {
                byte[] tmp = new byte[size];
                int i = st.Read(tmp, 0, tmp.Length);

                if (i <= 0)
                {
                    break;
                }

                Array.Resize<byte>(ref tmp, i);

                ret.Write(tmp);
            }

            ret.SeekToBegin();

            return ret;
        }
    }
}
