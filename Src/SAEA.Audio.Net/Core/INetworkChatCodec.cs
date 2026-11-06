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
*命名空间：SAEA.Audio.Core
*文件名： INetworkChatCodec
*版本号： v26.4.23.1
*唯一标识：0f727d67-5506-4a56-be93-236814cc3a6b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：INetworkChatCodec接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:33
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.Audio.Base.NAudio.Wave;
using System;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// 网络语音编解码器，负责 PCM 与压缩音频之间的互转
    /// </summary>
    public interface INetworkChatCodec : IDisposable
    {
        /// <summary>
        /// 编解码器的友好名称
        /// </summary>
        string Name { get; }
        /// <summary>
        /// 编解码器在当前系统上是否可用
        /// </summary>
        bool IsAvailable { get; }
        /// <summary>
        /// 编码后的比特率，单位 bit/s，无法确定时为 -1
        /// </summary>
        int BitsPerSecond { get; }
        /// <summary>
        /// 录音侧期望的 PCM 格式，通常为 8kHz 单声道 16 位
        /// </summary>
        WaveFormat RecordFormat { get; }
        /// <summary>
        /// 把一块 PCM 音频编码为压缩数据
        /// </summary>
        /// <param name="data">PCM 源缓冲区</param>
        /// <param name="offset">源数据起始偏移，单位字节</param>
        /// <param name="length">参与编码的字节数</param>
        /// <returns>新分配的编码结果，长度由编解码器决定</returns>
        byte[] Encode(byte[] data, int offset, int length);
        /// <summary>
        /// 把一块压缩音频解码为 PCM 数据
        /// </summary>
        /// <param name="data">压缩数据源缓冲区</param>
        /// <param name="offset">源数据起始偏移，单位字节</param>
        /// <param name="length">参与解码的字节数</param>
        /// <returns>新分配的解码结果，长度由编解码器决定</returns>
        byte[] Decode(byte[] data, int offset, int length);
    }
}
