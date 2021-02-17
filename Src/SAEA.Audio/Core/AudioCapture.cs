/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Core
*类 名 称：AudioCapture
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Audio.Base.NAudio.Wave;
using System;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// 语音捕获类
    /// </summary>
    public class AudioCapture : IDisposable
    {
        private readonly IWavePlayer _waveOut;
        private readonly BufferedWaveProvider _waveProvider;
        private readonly INetworkChatCodec _speexCodec;
        private readonly WaveIn _waveIn;

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

            _waveOut = new WaveOut();
            _waveOut.Init(_waveProvider);

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
        /// 打开播放设备
        /// </summary>
        public void Play()
        {
            _waveOut.Play();
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
        /// 暂停播放设备
        /// </summary>
        public void Pause()
        {
            _waveOut.Pause();
        }

        /// <summary>
        /// 开始捕获语音
        /// </summary>
        public void StartCaputure()
        {
            _waveIn.StartRecording();
        }

        /// <summary>
        /// 暂停捕获语音
        /// </summary>
        public void StopCaputure()
        {
            _waveIn.StopRecording();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopCaputure();
            _waveIn.Dispose();

            Pause();
            _waveOut.Dispose();
        }
    }
}
