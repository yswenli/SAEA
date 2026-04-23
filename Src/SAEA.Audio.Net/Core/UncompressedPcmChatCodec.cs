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
*文件名： UncompressedPcmChatCodec
*版本号： v26.4.23.1
*唯一标识：e6365a58-a17e-42cb-873f-fed0f826c920
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：UncompressedPcmChatCodec编解码类
*
*=====================================================================
*修改标记
*修改时间：2021/02/10 15:16:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UncompressedPcmChatCodec编解码类
*
*****************************************************************************/
using SAEA.Audio.Base.NAudio.Wave;
using System;

namespace SAEA.Audio.Core
{
    class UncompressedPcmChatCodec : INetworkChatCodec
    {
        public UncompressedPcmChatCodec()
        {
            RecordFormat = new WaveFormat(8000, 16, 1);
        }

        public string Name => "PCM 8kHz 16 bit uncompressed";

        public WaveFormat RecordFormat { get; private set; }

        public byte[] Encode(byte[] data, int offset, int length)
        {
            var encoded = new byte[length];
            Array.Copy(data, offset, encoded, 0, length);
            return encoded;
        }

        public byte[] Decode(byte[] data, int offset, int length)
        {
            var decoded = new byte[length];
            Array.Copy(data, offset, decoded, 0, length);
            return decoded;
        }

        public int BitsPerSecond => RecordFormat.AverageBytesPerSecond * 8;

        public void Dispose() { }

        public bool IsAvailable => true;
    }
}
