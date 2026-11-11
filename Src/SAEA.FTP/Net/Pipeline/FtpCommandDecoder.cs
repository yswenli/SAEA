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
*命名空间：SAEA.FTP
*文件名： FtpCommandDecoder
*版本号： v26.11.5.1
*唯一标识：65fb2db0-ddd0-4695-bf85-6850eb12485c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 14:30:00
*描述：服务端控制命令解码器
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 14:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增服务端控制命令解码器
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using SAEA.FTP.Model;
using SAEA.Sockets.Pipeline;

namespace SAEA.FTP
{
    /// <summary>
    /// 服务端控制命令解码器：把行分帧器产出的一行文本解成 <see cref="ClientRequest"/>。
    /// <para>
    /// 空行直接丢弃；命令文本先去掉首尾空白，再按原有规则拆分命令字与参数。
    /// </para>
    /// </summary>
    public sealed class FtpCommandDecoder : ByteToMessageDecoder
    {
        /// <summary>
        /// 解一行控制命令；空行不产生消息。
        /// </summary>
        /// <param name="ctx">当前处理器在链上的节点上下文。</param>
        /// <param name="frame">行分帧器切好的一行，已去掉 CRLF。</param>
        /// <param name="output">本次解码要按序派发的消息列表。</param>
        protected override void Decode(IChannelHandlerContext ctx, PooledMessage frame, List<object> output)
        {
            var bytes = frame.Frame;
            if (bytes.Length == 0)
                return;

            var line = Encoding.UTF8.GetString(bytes.ToArray()).Trim();
            if (line.Length == 0)
                return;

            var request = ClientRequest.Parse(line);
            if (request == null)
                return;

            // 保留原始命令文本，供服务端按原样记录日志。
            request.Raw = line;
            output.Add(request);
        }
    }
}