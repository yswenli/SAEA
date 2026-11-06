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
*命名空间：SAEA.Audio.Net
*文件名： TransferClient
*版本号： v26.4.23.1
*唯一标识：fd53f81b-9960-4625-a3e0-33aae44a8380
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：TransferClient接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:14:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：接收改为 OnClientReceiveSpan 与 DecodeStream，发送改用 SendAsync(ISocketProtocal)
*
*****************************************************************************/
using System;
using System.Net;
using System.Text;

using SAEA.Audio.Model;
using SAEA.Common.Serialization;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.Audio.Net
{
    /// <summary>
    /// 语音传输类
    /// </summary>
    public class TransferClient
    {
        IClientSocket _udpClient;

        BaseCoder _baseUnpacker;

        readonly ClientFrameHandler _frameHandler;

        readonly FileSpanHandler _onFileFrame;

        /// <summary>
        /// 收到语音数据帧时触发，参数为已解码的帧负载副本，可在回调之外持有
        /// </summary>
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

        /// <summary>
        /// 语音传输类
        /// </summary>
        /// <param name="endPoint"></param>
        public TransferClient(IPEndPoint endPoint)
        {
            var bContext = new BaseContext<BaseCoder>();

            _udpClient = SocketFactory.CreateClientSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UsePacket(bContext)
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .Build());

            _baseUnpacker = (BaseCoder)bContext.Unpacker;

            _frameHandler = new ClientFrameHandler(this);

            _onFileFrame = OnFileFrame;

            _udpClient.OnClientReceiveSpan += _udpClient_OnReceiveSpan;
        }

        private void _udpClient_OnReceiveSpan(ReadOnlySpan<byte> data)
        {
            // data 仅在回调期间有效：DecodeStream 同步消费完即返回，不跨回调持有。
            _baseUnpacker.DecodeStream(data, _frameHandler, null, _onFileFrame);
        }

        private void OnFileFrame(ReadOnlySpan<byte> content)
        {
            // 类型码 3 与 SAEA 的大帧类型重合，会被拆帧器归类为文件帧；本协议中它就是 Agree。
            DispatchFrame((byte)ProtocalType.Agree, content);
        }

        private void DispatchFrame(byte type, ReadOnlySpan<byte> content)
        {
            switch ((ProtocalType)type)
            {
                case ProtocalType.Ping:

                    break;
                case ProtocalType.Pong:

                    break;
                case ProtocalType.Invite:
                    var ii = SerializeHelper.Deserialize<InvitedInfo>(Encoding.UTF8.GetString(content.ToArray()));
                    OnInvited?.Invoke(ii);
                    Channel = ii.ChannelID;
                    break;
                case ProtocalType.Agree:
                    var ai = SerializeHelper.Deserialize<AgreeInfo>(Encoding.UTF8.GetString(content.ToArray()));
                    OnAgree?.Invoke(ai);
                    Channel = ai.ChannelID;
                    break;
                case ProtocalType.Disagree:
                    OnDisagree?.Invoke(Encoding.UTF8.GetString(content.ToArray()));
                    break;
                case ProtocalType.Join:
                    var ji = SerializeHelper.Deserialize<JoinInfo>(Encoding.UTF8.GetString(content.ToArray()));
                    OnJoin?.Invoke(ji);
                    Channel = ji.ChannelID;
                    break;
                case ProtocalType.Data:
                    OnReceive?.Invoke(content.ToArray());
                    break;
            }
        }

        /// <summary>
        /// 尝试建立udp
        /// </summary>
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
            // 帧头由编码器按 ISocketProtocal 补齐，这里只构造协议对象。
            var content = data == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(data);
            _udpClient.SendAsync(new BaseSocketProtocal((byte)protocalType, content));
        }

        /// <summary>
        /// 发送心跳包，无负载
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
            BaseSend(ProtocalType.Join, Encoding.UTF8.GetBytes(channelID));
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

        /// <summary>
        /// 断开底层 UDP 客户端连接
        /// </summary>
        public void Disconnect()
        {
            _udpClient.Disconnect();
        }

        /// <summary>
        /// 把解码出的帧回调到所属实例；帧体仅在回调期间有效。
        /// </summary>
        private sealed class ClientFrameHandler : IFrameHandler
        {
            readonly TransferClient _owner;

            public ClientFrameHandler(TransferClient owner)
            {
                _owner = owner;
            }

            public void OnFrame(in SocketFrame frame)
            {
                _owner.DispatchFrame(frame.Type, frame.Content);
            }
        }

    }
}
