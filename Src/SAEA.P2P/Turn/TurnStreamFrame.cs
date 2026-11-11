/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.Turn
*文件名： TurnStreamFrame
*版本号： v26.11.5.1
*唯一标识：46d3d509-4f59-426d-96eb-4216fc65e3b2
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 01:30:00
*描述：TURN 控制连接上解析出的一条完整帧
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 01:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 控制连接上解析出的一条完整帧
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Turn
{
    /// <summary>
    /// TURN 控制连接上解析出的一条完整帧：类型加原始字节。
    /// 字节数组由解析器按帧新建，调用方可以直接持有，不会在下一次解析时被覆盖。
    /// </summary>
    public sealed class TurnStreamFrame
    {
        /// <summary>
        /// 帧的原始字节
        /// </summary>
        private readonly byte[] _data;

        /// <summary>
        /// 创建一条帧
        /// </summary>
        /// <param name="kind">帧类型</param>
        /// <param name="data">帧的完整字节</param>
        /// <exception cref="ArgumentNullException">data 为 null</exception>
        public TurnStreamFrame(TurnStreamFrameKind kind, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            Kind = kind;
            _data = data;
        }

        /// <summary>
        /// 帧类型
        /// </summary>
        public TurnStreamFrameKind Kind { get; private set; }

        /// <summary>
        /// 帧的完整字节：STUN 为 20 字节头加属性区，ChannelData 为 4 字节头加载荷（可能含补齐字节）。
        /// 长度字段仍按自身语义解析，补齐字节不计入载荷。
        /// </summary>
        public byte[] Data
        {
            get { return _data; }
        }
    }
}