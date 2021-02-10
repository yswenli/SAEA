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
    public class SocketClient
    {
        IClientSocket _udpClient;

        BaseUnpacker _baseUnpacker;

        public event Action<Byte[]> OnReceive;

        private string _channelID = "";

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

        public SocketClient(IPEndPoint endPoint)
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
                        OnInvited?.Invoke(SerializeHelper.Deserialize<InvitedInfo>(Encoding.UTF8.GetString(p.Content)));
                        break;
                    case ProtocalType.Agree:
                        OnAgree?.Invoke(SerializeHelper.Deserialize<AgreeInfo>(Encoding.UTF8.GetString(p.Content)));
                        break;
                    case ProtocalType.Disagree:
                        OnDisagree?.Invoke(Encoding.UTF8.GetString(p.Content));
                        break;
                    case ProtocalType.Join:
                        OnJoin?.Invoke(SerializeHelper.Deserialize<JoinInfo>(Encoding.UTF8.GetString(p.Content)));
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
        /// <param name="id"></param>
        /// <param name="channelID"></param>
        public void Agree(string id, string channelID)
        {
            var at = new AgreeInfo()
            {
                ID = id,
                ChannelID = channelID
            };
            BaseSend(ProtocalType.Agree, Encoding.UTF8.GetBytes(SerializeHelper.Serialize(at)));
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
        public void Send(string channelID, byte[] data)
        {
            BaseSend(ProtocalType.Data, SAEASerialize.Serialize(new DataInfo()
            {
                ChannelID = channelID,
                Data = data
            }));
        }

        public void Send(byte[] data)
        {
            _udpClient.SendAsync(data);
        }

        public void Disconnect()
        {
            _udpClient.Disconnect();
        }

    }
}
