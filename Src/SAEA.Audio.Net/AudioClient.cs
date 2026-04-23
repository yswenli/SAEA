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
*命名空间：SAEA.Audio
*文件名： AudioClient
*版本号： v26.4.23.1
*唯一标识：ce8add45-b0b4-456d-96b6-a179bea8f7c9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：
*
*=====================================================================
*修改标记
*修改时间：2021/02/10 15:16:38
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Net;

using SAEA.Audio.Core;
using SAEA.Audio.Net;

namespace SAEA.Audio
{
    /// <summary>
    /// 语音客户端
    /// </summary>
    public class AudioClient : IDisposable
    {
        TransferClient _transferClient;

        AudioCapture _audioCapture;

        AudioPlayer _audioPlayer;

        /// <summary>
        /// 语音客户端
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="quality"></param>
        public AudioClient(IPEndPoint endPoint, int quality = 2)
        {
            _transferClient = new TransferClient(endPoint);
            _transferClient.OnReceive += _audioClient_OnReceive;

            _audioCapture = new AudioCapture(quality, 50);
            _audioCapture.OnAudioCaptured += _audioCapture_OnAudioCaptured;

            _audioPlayer = new AudioPlayer(quality, 50);
        }


        /// <summary>
        /// 语音客户端
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="quality"></param>
        public AudioClient(string ip, int port, int quality = 2) : this(new IPEndPoint(IPAddress.Parse(ip), port), quality)
        {

        }

        /// <summary>
        /// 邀请
        /// </summary>
        /// <param name="id"></param>
        public void Invite(string id)
        {
            _transferClient.Invite(id);
        }

        /// <summary>
        /// 同意
        /// </summary>
        public void Agree()
        {
            _transferClient.Agree();
        }

        /// <summary>
        /// 拒绝
        /// </summary>
        public void Disagree()
        {
            _transferClient.Disagree();
        }

        /// <summary>
        /// 加入
        /// </summary>
        /// <param name="channelID"></param>
        public void Join(string channelID)
        {
            _transferClient.Join(channelID);
        }

        /// <summary>
        /// 退出
        /// </summary>
        public void Quit()
        {
            _transferClient.Quit();
        }

        /// <summary>
        /// 启动语音
        /// </summary>
        public void Start()
        {
            _transferClient.Connect();

            _audioPlayer.Play();

            _audioCapture.Start();
        }

        private void _audioClient_OnReceive(byte[] data)
        {
            _audioCapture.Resolving(data);
        }


        private void _audioCapture_OnAudioCaptured(object sender, byte[] data)
        {
            _transferClient.SendData(data);
        }

        /// <summary>
        /// 关闭语音
        /// </summary>
        public void Stop()
        {
            _audioPlayer.Stop();
            _audioCapture.Stop();
            _transferClient.Disconnect();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _audioPlayer.Dispose();
            _audioCapture.Dispose();
        }
    }
}
