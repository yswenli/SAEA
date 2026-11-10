/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.FileSocket.Tests.Tests.Support
*文件名： FileFrameBuilder
*版本号： net10.0
*唯一标识：bb22b8d6-0a1b-401e-aa3d-4bcfe22b73d1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/10 12:05:00
*描述：按文件传输线格式拼装测试用原始帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/10 12:05:00
*修改人： yswenli
*版本号： net10.0
*描述：新增，按文件传输线格式拼装测试用原始帧
*
*****************************************************************************/
using System;
using System.Buffers.Binary;

namespace SAEA.FileSocket.Tests.Tests.Support
{
    /// <summary>
    /// 文件传输线格式 <c>[8B bodyLength 小端][1B type][body]</c> 的拼装器，只用于测试输入。
    /// </summary>
    public static class FileFrameBuilder
    {
        /// <summary>
        /// 按帧类型与帧体拼出一帧完整线格式字节。
        /// </summary>
        /// <param name="type">帧类型字节。</param>
        /// <param name="body">帧体，可为 null 表示空帧体。</param>
        /// <returns>含 9 字节帧头的完整帧。</returns>
        public static byte[] Build(byte type, byte[] body)
        {
            var length = body == null ? 0 : body.Length;

            var frame = new byte[9 + length];
            BinaryPrimitives.WriteInt64LittleEndian(frame.AsSpan(0, 8), length);
            frame[8] = type;

            if (length > 0)
                Buffer.BlockCopy(body, 0, frame, 9, length);

            return frame;
        }

        /// <summary>
        /// 拼出一段指定长度的可预测帧体：第 i 个字节为 <c>(byte)i</c>。
        /// </summary>
        /// <param name="length">帧体长度。</param>
        /// <returns>填充后的帧体。</returns>
        public static byte[] Payload(int length)
        {
            var body = new byte[length];
            for (var i = 0; i < length; i++)
                body[i] = (byte)i;

            return body;
        }
    }
}