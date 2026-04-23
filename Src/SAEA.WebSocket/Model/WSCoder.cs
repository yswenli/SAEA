/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.WebSocket.Model
*文件名： WSCoder
*版本号： v26.4.23.1
*唯一标识：ba3ea3ae-d654-4bd7-b04e-40f0aed57aa5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：WSCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：WSCoder编解码类
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SAEA.Common;
using SAEA.Common.Caching;
using SAEA.Sockets.Interface;

namespace SAEA.WebSocket.Model
{
    /// <summary>
    /// 编码协议
    /// </summary>
    public class WSCoder : ICoder
    {
        private List<byte> _buffer;

        /// <summary>
        /// 编码协议
        /// </summary>
        public WSCoder()
        {
            _buffer = new List<byte>(4096);
        }

        /// <summary>
        /// 编码协议数据
        /// </summary>
        /// <param name="protocal">协议数据</param>
        /// <returns>编码后的字节数组</returns>
        public byte[] Encode(ISocketProtocal protocal)
        {
            return protocal.ToBytes();
        }

        /// <summary>
        /// 解码数据
        /// </summary>
        /// <param name="data">接收到的数据</param>
        /// <param name="onHeart">心跳回调</param>
        /// <param name="onFile">文件回调</param>
        public List<ISocketProtocal> Decode(byte[] data, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            return Decode(data);
        }

        /// <summary>
        /// 解码数据的具体实现
        /// </summary>
        /// <param name="data">接收到的数据</param>
        private List<ISocketProtocal> Decode(byte[] data)
        {
            _buffer.AddRange(data);
            List<ISocketProtocal> result = new List<ISocketProtocal>();
            try
            {
                while (_buffer.Count > 3)
                {
                    byte[] payloadData;
                    var opcode = (byte)(_buffer[0] & 0x0f);
                    bool mask = (_buffer[1] & 0x80) == 0x80; // 是否包含掩码  
                    int payloadLen = _buffer[1] & 0x7F; // 数据长度
                    var buffer = _buffer.ToArray();
                    if (payloadLen + 2 > _buffer.Count) break;
                    if (mask)
                    {
                        // masks只有4字节，使用stackalloc避免堆分配
                        unsafe
                        {
                            byte* masks = stackalloc byte[4];
                            if (payloadLen == 126)
                            {
                                var len = (ushort)(_buffer[2] << 8 | _buffer[3]);
                                if (len + 8 > _buffer.Count) break;
                                
                                // 复制masks到stackalloc
                                for (int i = 0; i < 4; i++)
                                    masks[i] = buffer[4 + i];
                                
                                // 大数据使用内存池，小数据直接分配
                                if (len > MemoryPoolManager.SmallThreshold)
                                {
                                    payloadData = MemoryPoolManager.Rent(len);
                                    Buffer.BlockCopy(buffer, 8, payloadData, 0, len);
                                }
                                else
                                {
                                    payloadData = new byte[len];
                                    Buffer.BlockCopy(buffer, 8, payloadData, 0, len);
                                }
                                
                                DoMaskUnsafe(payloadData, 0, len, masks);
                                result.Add(new WSProtocal(opcode, payloadData) { IsPooled = len > MemoryPoolManager.SmallThreshold });
                                if (_buffer.Count >= 8 + len)
                                    _buffer.RemoveRange(0, 8 + len);
                                else
                                    break;
                            }
                            else
                            {
                                if (_buffer.Count < 6 + payloadLen) break;
                                
                                // 复制masks到stackalloc
                                for (int i = 0; i < 4; i++)
                                    masks[i] = buffer[2 + i];
                                
                                // 大数据使用内存池，小数据直接分配
                                if (payloadLen > MemoryPoolManager.SmallThreshold)
                                {
                                    payloadData = MemoryPoolManager.Rent(payloadLen);
                                    Buffer.BlockCopy(buffer, 6, payloadData, 0, payloadLen);
                                }
                                else
                                {
                                    payloadData = new byte[payloadLen];
                                    Buffer.BlockCopy(buffer, 6, payloadData, 0, payloadLen);
                                }
                                
                                DoMaskUnsafe(payloadData, 0, payloadLen, masks);
                                result.Add(new WSProtocal(opcode, payloadData) { IsPooled = payloadLen > MemoryPoolManager.SmallThreshold });
                                if (_buffer.Count >= 6 + payloadLen)
                                    _buffer.RemoveRange(0, 6 + payloadLen);
                                else
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (payloadLen == 126)
                        {
                            var len = (ushort)(buffer[2] << 8 | buffer[3]);
                            if (len + 8 > buffer.Length) break;
                            
                            // 大数据使用内存池，小数据直接分配
                            if (len > MemoryPoolManager.SmallThreshold)
                            {
                                payloadData = MemoryPoolManager.Rent(len);
                            }
                            else
                            {
                                payloadData = new byte[len];
                            }
                            Buffer.BlockCopy(buffer, 4, payloadData, 0, len);
                            result.Add(new WSProtocal(opcode, payloadData) { IsPooled = len > MemoryPoolManager.SmallThreshold });
                            if (_buffer.Count >= 4 + len)
                                _buffer.RemoveRange(0, 4 + len);
                            else
                                break;
                        }
                        else
                        {
                            if (_buffer.Count < 2 + payloadLen) break;
                            
                            // 大数据使用内存池，小数据直接分配
                            if (payloadLen > MemoryPoolManager.SmallThreshold)
                            {
                                payloadData = MemoryPoolManager.Rent(payloadLen);
                            }
                            else
                            {
                                payloadData = new byte[payloadLen];
                            }
                            Buffer.BlockCopy(buffer, 2, payloadData, 0, payloadLen);
                            result.Add(new WSProtocal(opcode, payloadData) { IsPooled = payloadLen > MemoryPoolManager.SmallThreshold });
                            if (_buffer.Count >= 2 + payloadLen)
                                _buffer.RemoveRange(0, 2 + payloadLen);
                            else
                                break;
                        }
                    }
                }
            }
            catch(Exception ex)
            {

            }
            return result;
        }

        /// <summary>
        /// 对数据进行掩码处理
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">数据长度</param>
        /// <param name="masks">掩码</param>
        public static void DoMask(byte[] buffer, int offset, int length, byte[] masks)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }
        }

        /// <summary>
        /// 对数据进行掩码处理（使用stackalloc的unsafe版本）
        /// </summary>
        /// <param name="buffer">数据缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="length">数据长度</param>
        /// <param name="masks">掩码指针</param>
        unsafe public static void DoMaskUnsafe(byte[] buffer, int offset, int length, byte* masks)
        {
            for (var i = 0; i < length; i++)
            {
                buffer[offset + i] = (byte)(buffer[offset + i] ^ masks[i % 4]);
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }
    }
}
