/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Core
*类 名 称：AudioPlayer
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/5 15:43:29
*描述：
*=====================================================================
*修改时间：2019/11/5 15:43:29
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

using System;

using SAEA.Audio.Base.NAudio.Wave;

namespace SAEA.Audio.Core
{
    /// <summary>
    /// 语音播放器
    /// </summary>
    public class AudioPlayer:IDisposable
    {
        private readonly INetworkChatCodec _speexCodec;

        private readonly BufferedWaveProvider _waveProvider;

        private readonly IWavePlayer _waveOut;

        /// <summary>
        /// 语音播放器
        /// </summary>
        /// <param name="quality"></param>
        /// <param name="bufferTime"></param>
        public AudioPlayer(int quality, int bufferTime = 50)
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
        }

        /// <summary>
        /// 打开播放设备
        /// </summary>
        public void Play()
        {
            _waveOut.Play();
        }

        /// <summary>
        /// 暂停播放设备
        /// </summary>
        public void Pause()
        {
            _waveOut.Pause();
        }
        /// <summary>
        /// 停止播放设备
        /// </summary>
        public void Stop()
        {
            _waveOut.Stop();
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _waveOut?.Dispose();
        }
    }
}
