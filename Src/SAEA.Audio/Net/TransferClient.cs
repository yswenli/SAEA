/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Net
*类 名 称：TransferClient
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
using SAEA.Audio.Model;
using SAEA.Common.Serialization;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Model;
using System;
using System.Net;
using System.Text;

namespace SAEA.Audio.Net
{
    public class TransferClient
    {
        IClientSocket _udpClient;

        BaseUnpacker _baseUnpacker;

        public event Action<Byte[]> OnReceive;

        /// <summary>
        /// 频道
        /// </summary>
        public string Channel { get; private set; }

        /// <summary>
        /// 被邀请事件
        /// </summary>
        public Action<InvitedInfo> OnInvited;

        /// <summary>
        /// 收到同意事件
        /// </summary>
        public Action<AgreeInfo> OnAgree;

        /// <summary>
        /// 拒绝事件
        /// </summary>
        public Action<string> OnDisagree;

        /// <summary>
        /// 加入事件
        /// </summary>
        public Action<JoinInfo> OnJoin;

        public TransferClient(IPEndPoint endPoint)
        {
            var bContext = new BaseContext();

            _udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UseIocp(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _baseUnpacker = (BaseUnpacker)bContext.Unpacker;

            _udpClient.OnReceive += _udpClient_OnReceive;
        }

        private void _udpClient_OnReceive(byte[] data)
        {
            _baseUnpacker.Unpack(data, (p) =>
            {
                var protocalType = (ProtocalType)p.Type;

                switch (protocalType)
                {
                    case ProtocalType.Ping:

                        break;
                    case ProtocalType.Pong:

                        break;
                    case ProtocalType.Invite:
                        var ii = SerializeHelper.Deserialize<InvitedInfo>(Encoding.UTF8.GetString(p.Content));
                        OnInvited?.Invoke(ii);
                        Channel = ii.ChannelID;
                        break;
                    case ProtocalType.Agree:
                        var ai = SerializeHelper.Deserialize<AgreeInfo>(Encoding.UTF8.GetString(p.Content));
                        OnAgree?.Invoke(ai);
                        Channel = ai.ChannelID;
                        break;
                    case ProtocalType.Disagree:
                        OnDisagree?.Invoke(Encoding.UTF8.GetString(p.Content));
                        break;
                    case ProtocalType.Join:
                        var ji = SerializeHelper.Deserialize<JoinInfo>(Encoding.UTF8.GetString(p.Content));
                        OnJoin?.Invoke(ji);
                        Channel = ji.ChannelID;
                        break;
                    case ProtocalType.Data:
                        OnReceive?.Invoke(p.Content);
                        break;
                }
            }, null, null);
        }

        public void Connect()
        {
            _udpClient.Connect();
        }

        /// <summary>
        /// 基础发送数据
        /// </summary>
        /// <param name="protocalType"></param>
        /// <param name="data"></param>
        void BaseSend(ProtocalType protocalType, byte[] data)
        {
            var p = new BaseSocketProtocal();
            p.Type = (byte)protocalType;
            p.Content = data;
            if (p.Content != null)
            {
                p.BodyLength = p.Content.Length;
            }
            else
            {
                p.BodyLength = 0;
            }
            _udpClient.SendAsync(p.ToBytes());
        }

        /// <summary>
        /// ping
        /// </summary>
        public void Ping()
        {
            BaseSend(ProtocalType.Ping, null);
        }

        /// <summary>
        /// 邀请
        /// </summary>
        /// <param name="id"></param>
        public void Invite(string id)
        {
            BaseSend(ProtocalType.Invite, Encoding.UTF8.GetBytes(id));
        }

        /// <summary>
        /// 同意
        /// </summary>
        public void Agree()
        {
            BaseSend(ProtocalType.Agree, null);
        }

        /// <summary>
        /// 拒绝
        /// </summary>
        public void Disagree()
        {
            BaseSend(ProtocalType.Disagree, null);
        }

        /// <summary>
        /// 加入
        /// </summary>
        /// <param name="channelID"></param>
        public void Join(string channelID)
        {
            BaseSend(ProtocalType.Agree, Encoding.UTF8.GetBytes(channelID));
        }

        /// <summary>
        /// 发送语音数据
        /// </summary>
        /// <param name="channelID"></param>
        /// <param name="data"></param>
        public void SendData(string channelID, byte[] data)
        {
            if (!string.IsNullOrEmpty(channelID))
                BaseSend(ProtocalType.Data, SAEASerialize.Serialize(new DataInfo()
                {
                    ChannelID = channelID,
                    Data = data
                }));
        }
        /// <summary>
        /// 发送语音数据
        /// </summary>
        /// <param name="data"></param>
        public void SendData(byte[] data)
        {
            SendData(Channel, data);
        }

        /// <summary>
        /// 退出
        /// </summary>
        public void Quit()
        {
            BaseSend(ProtocalType.Quit, null);
        }


        public void Disconnect()
        {
            _udpClient.Disconnect();
        }

    }
}
