/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio
*类 名 称：AudioClient
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
