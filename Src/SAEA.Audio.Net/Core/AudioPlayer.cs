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
*文件名： AudioPlayer
*版本号： v26.4.23.1
*唯一标识：ad891f1b-52ef-4e9a-912f-109dd1002ee2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/09/20 01:32:10
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/09/20 01:32:10
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
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
