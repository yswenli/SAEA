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
*文件名： AudioCapture
*版本号： v26.4.23.1
*唯一标识：b449cf92-cc1b-449f-aff9-d039790f0c81
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：AudioCapture接口
*
*=====================================================================
*修改标记
*修改时间：2021/02/21 16:29:07
*修改人： yswenli
*版本号： v26.4.23.1
*描述：AudioCapture接口
*
*****************************************************************************/
using System;

using SAEA.Audio.Base.NAudio.Wave;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// 语音捕获类
    /// </summary>
    public class AudioCapture : IDisposable
    {
        
        private readonly INetworkChatCodec _speexCodec;

        private readonly WaveIn _waveIn;

        private readonly BufferedWaveProvider _waveProvider;

        /// <summary>
        /// 语音数据
        /// </summary>
        public event EventHandler<byte[]> OnAudioCaptured;

        /// <summary>
        /// 语音捕获类
        /// </summary>
        /// <param name="quality">1为低质量、2为一般默认、3为高质量</param>
        /// <param name="bufferTime"></param>
        public AudioCapture(int quality, int bufferTime = 50)
        {
            switch (quality)
            {
                case 1:
                    _speexCodec = new NarrowBandSpeexCodec();
                    break;
                case 3:
                    _speexCodec = new UltraWideBandSpeexCodec();
                    break;
                case 2:
                default:
                    _speexCodec = new WideBandSpeexCodec();
                    break;
            }

            _waveProvider = new BufferedWaveProvider(_speexCodec.RecordFormat);

            _waveIn = new WaveIn();
            _waveIn.BufferMilliseconds = bufferTime;
            _waveIn.DeviceNumber = 0;
            _waveIn.WaveFormat = _speexCodec.RecordFormat;
            _waveIn.DataAvailable += _waveIn_DataAvailable;
        }

        private void _waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            OnAudioCaptured?.Invoke(this, _speexCodec.Encode(e.Buffer, 0, e.BytesRecorded));
        }
       

        /// <summary>
        /// 解析收到的数据
        /// </summary>
        /// <param name="enData"></param>
        public void Resolving(byte[] enData)
        {
            byte[] decoded = _speexCodec.Decode(enData, 0, enData.Length);
            _waveProvider.AddSamples(decoded, 0, decoded.Length);
        }       

        /// <summary>
        /// 开始捕获语音
        /// </summary>
        public void Start()
        {
            _waveIn.StartRecording();
        }

        /// <summary>
        /// 暂停捕获语音
        /// </summary>
        public void Stop()
        {
            _waveIn.StopRecording();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _waveIn.StopRecording();
            _waveIn.Dispose();
        }
    }
}
