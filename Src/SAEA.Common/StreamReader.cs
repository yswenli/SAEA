/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： StreamReader
*版本号： V3.0.0.1
*唯一标识：0957f3bb-7462-4ff0-867d-0a8c9411f2eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 9:33:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 9:33:39
*修改人： yswenli
*版本号： V3.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SAEA.Common
{
    /// <summary>
    /// 流读取
    /// </summary>
    public class StreamReader : IDisposable
    {
        Stream _stream;

        int _position = 0;

        int _bufferSize = 1024;

        List<byte> _cache = new List<byte>();

        /// <summary>
        /// 当前流位置
        /// </summary>
        public int Position
        {
            get
            {
                _position = (int)_stream.Position;
                return _position;
            }
            set
            {
                _position = value;
                _stream.Position = _position;
            }
        }

        /// <summary>
        /// 流读取
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="position"></param>
        /// <param name="bufferSize"></param>
        public StreamReader(Stream stream, int position = 0, int bufferSize = 1024)
        {
            _stream = stream;
            _stream.Position = position;
            _bufferSize = bufferSize;
        }

        /// <summary>
        /// 读取一行数据
        /// 若无数据则为null
        /// </summary>
        /// <returns></returns>
        public string ReadLine()
        {
            var result = string.Empty;
            int data = 0;
            do
            {
                data = _stream.ReadByte();

                if (data == 13 && _stream.ReadByte() == 10)
                {
                    if (_cache.Count > 0)
                    {
                        result = Encoding.UTF8.GetString(_cache.ToArray());
                    }
                    _cache.Clear();

                    return result;
                }
                else if (data == -1 && _cache.Count == 0)
                {
                    return null;
                }
                _cache.Add((byte)data);
            }
            while (data >= 0);

            return result;
        }

        /// <summary>
        /// 根据分隔符查找
        /// </summary>
        /// <param name="position"></param>
        /// <param name="bounderyData"></param>
        /// <returns></returns>
        public byte[] ReadData(int position, byte[] bounderyData)
        {
            _stream.Position = position;

            List<byte> list = new List<byte>();
            do
            {
                var data = ReadData((int)_stream.Position);
                if (data != null)
                {
                    list.AddRange(data);

                    var calcData = list.ToArray();

                    var index = calcData.IndexOf(bounderyData);

                    Array.Clear(calcData, 0, calcData.Length);
                    calcData = null;

                    if (index > 0)
                    {
                        _stream.Position = position + index;
                        list.RemoveRange(index, list.Count - index);
                        break;
                    }

                }
                else
                {
                    break;
                }
            }
            while (this.Position < _stream.Length);
            var result = list.ToArray();
            list.Clear();
            return result;
        }
        /// <summary>
        /// 读取数据
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public byte[] ReadData(int position)
        {
            _stream.Position = position;
            var data = new byte[_bufferSize];
            var len = _stream.Read(data, 0, data.Length);
            if (len > 0)
            {
                if (len == 1024)
                {
                    return data;
                }
                var ldata = new byte[len];
                Buffer.BlockCopy(data, 0, ldata, 0, len);
                Array.Clear(data, 0, data.Length);
                data = null;
                return ldata;
            }
            return null;
        }

        /// <summary>
        /// 根据分隔符查找
        /// </summary>
        /// <param name="position"></param>
        /// <param name="boundery"></param>
        /// <returns></returns>
        public byte[] ReadData(int position, string boundery)
        {
            var bounderyData = Encoding.ASCII.GetBytes(boundery);
            return ReadData(position, bounderyData);
        }
        /// <summary>
        /// 释入所占用资源
        /// </summary>
        public void Dispose()
        {
            _cache.Clear();
            _position = 0;
            _stream.Close();
        }
    }
}
