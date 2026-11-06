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
*文件名： TransferServer
*版本号： v26.4.23.1
*唯一标识：f0525faa-38e2-4149-8703-4cb2d1758e1b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/21 16:29:07
*描述：TransferServer服务端类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 13:14:00
*修改人： yswenli
*版本号： v26.4.23.1
*描述：接收改为 OnClientReceiveSpan 与 DecodeStream，发送改用 SendAsync(ISocketProtocal)
*
*****************************************************************************/
using SAEA.Audio.Model;
using SAEA.Audio.Storage;
using SAEA.Common.Serialization;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Audio.Net
{
    /// <summary>
    /// 语音传输服务器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TransferServer<T> where T : IStorage
    {
        IServerSocket _udpServer;

        ConcurrentDictionary<string, IUserToken> _cache;


        ConcurrentDictionary<string, string> _invitedMapping;

        IStorage _storage;

        /// <summary>
        /// OnReceive
        /// </summary>
        public event Action<DataInfo> OnReceive;

        /// <summary>
        /// 语音传输服务器
        /// </summary>
        /// <param name="endPoint"></param>
        public TransferServer(IPEndPoint endPoint)
        {
            _cache = new ConcurrentDictionary<string, IUserToken>();

            _invitedMapping = new ConcurrentDictionary<string, string>();

            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UsePacket()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetActionTimeOut(60 * 1000)
                .Build());
            _udpServer.OnAccepted += _udpServer_OnAccepted;
            _udpServer.OnDisconnected += _udpServer_OnDisconnected;
            _udpServer.OnServerReceiveSpan += _udpServer_OnReceiveSpan;

            _storage = StorageFactory.Create<T>();
        }


        public void Start()
        {
            _udpServer.Start();
        }


        public void Stop()
        {
            _udpServer.Stop();
        }


        private void _udpServer_OnReceiveSpan(IUserToken userToken, ReadOnlySpan<byte> data)
        {
            var frameCoder = userToken?.Coder as IFrameCoder;
            if (frameCoder == null) return;

            var handler = new ServerFrameHandler(this, userToken.ID);

            // data 仅在回调期间有效：DecodeStream 同步消费完即返回，不跨回调持有。
            frameCoder.DecodeStream(data, handler, null, handler.OnFile);
        }

        private void DispatchFrame(string userTokenId, byte type, ReadOnlySpan<byte> content)
        {
            switch ((ProtocalType)type)
            {
                case ProtocalType.Ping:
                    BaseReply(userTokenId, ProtocalType.Pong, null);
                    break;
                case ProtocalType.Pong:

                    break;
                case ProtocalType.Invite:
                    ReplyInvite(userTokenId, content.ToArray());
                    break;
                case ProtocalType.Agree:
                    ReplyAgree(userTokenId);
                    break;
                case ProtocalType.Disagree:
                    ReplyDidagree(userTokenId);
                    break;
                case ProtocalType.Join:
                    ReplyJoin(userTokenId, content.ToArray());
                    break;
                case ProtocalType.Data:
                    ReplyData(content.ToArray());
                    break;
                case ProtocalType.Quit:
                    ReplyQuit(userTokenId);
                    break;
            }
        }

        private void _udpServer_OnAccepted(object obj)
        {
            var ut = (IUserToken)obj;
            if (ut != null)
            {
                _cache.TryAdd(ut.ID, ut);
            }
        }

        void BaseReply(string id, ProtocalType protocalType, byte[] data)
        {
            // 帧头由编码器按 ISocketProtocal 补齐，这里只构造协议对象。
            var content = data == null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(data);
            _udpServer.SendAsync(id, new BaseSocketProtocal((byte)protocalType, content));
        }

        /// <summary>
        /// 处理邀请
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="data"></param>
        void ReplyInvite(string userID, byte[] data)
        {
            var id = Encoding.UTF8.GetString(data);

            var channelID = _storage.GetOrAddUserChannelMapping(userID);

            var it = new InvitedInfo()
            {
                ID = userID,
                ChannelID = channelID
            };

            BaseReply(id, ProtocalType.Invite, Encoding.UTF8.GetBytes(SerializeHelper.Serialize(it)));

            _invitedMapping.TryAdd(id, userID);
        }

        /// <summary>
        /// 处理同意
        /// </summary>
        /// <param name="userID"></param>
        void ReplyAgree(string userID)
        {
            if (_invitedMapping.TryRemove(userID, out string id))
            {
                var channelID = _storage.GetUserChannelMapping(id);

                if (!string.IsNullOrEmpty(channelID))
                {
                    var at = new AgreeInfo()
                    {
                        ID = userID,
                        ChannelID = channelID
                    };

                    BaseReply(id, ProtocalType.Agree, Encoding.UTF8.GetBytes(SerializeHelper.Serialize(at)));

                    _storage.SetUserChannelMapping(userID, channelID);

                    _invitedMapping.TryRemove(userID, out string _);

                    _storage.SetChannelUserMapping(channelID, userID, id);
                }
            }
        }

        /// <summary>
        /// 处理拒绝
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="data"></param>
        void ReplyDidagree(string userID)
        {
            if (_invitedMapping.TryRemove(userID, out string id))
            {
                if (_storage.ExistsUserChannelMapping(id))
                {
                    BaseReply(id, ProtocalType.Disagree, Encoding.UTF8.GetBytes(userID));
                }
            }
        }

        /// <summary>
        /// 处理退出
        /// </summary>
        /// <param name="userID"></param>
        void ReplyQuit(string userID)
        {
            string channelID = _storage.TryRemoveUserChannelMapping(userID);

            if (!string.IsNullOrEmpty(channelID))
            {
                _storage.TryRemoveChannelUserMapping(channelID, userID);
            }
        }

        /// <summary>
        /// 处理加入
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="data"></param>
        void ReplyJoin(string userID, byte[] data)
        {
            var channelID = Encoding.UTF8.GetString(data);

            _storage.SetUserChannelMapping(userID, channelID);

            _storage.SetChannelUserMapping(channelID, userID);

            var joinInfo = new JoinInfo()
            {
                ID = userID,
                ChannelID = channelID
            };

            BaseReply(userID, ProtocalType.Join, Encoding.UTF8.GetBytes(SerializeHelper.Serialize(joinInfo)));
        }

        void ReplyData(byte[] data)
        {
            var dt = SAEASerialize.Deserialize<DataInfo>(data);

            OnReceive?.Invoke(dt);

            var userIDs = _storage.GetChannelUsers(dt.ChannelID);

            Parallel.ForEach(userIDs, (id) =>
            {
                BaseReply(id, ProtocalType.Data, dt.Data);
            });
        }


        private void _udpServer_OnDisconnected(string ID, Exception ex)
        {
            _cache.TryRemove(ID, out IUserToken _);
        }

        /// <summary>
        /// 把解码出的帧回调到所属实例；帧体仅在回调期间有效。
        /// </summary>
        private sealed class ServerFrameHandler : IFrameHandler
        {
            readonly TransferServer<T> _owner;

            readonly string _userTokenId;

            public ServerFrameHandler(TransferServer<T> owner, string userTokenId)
            {
                _owner = owner;
                _userTokenId = userTokenId;
            }

            public void OnFrame(in SocketFrame frame)
            {
                _owner.DispatchFrame(_userTokenId, frame.Type, frame.Content);
            }

            /// <summary>
            /// 类型码 3 与 SAEA 的大帧类型重合，会被拆帧器归类为文件帧；本协议中它就是 Agree。
            /// </summary>
            /// <param name="content">帧体，仅在回调期间有效。</param>
            public void OnFile(ReadOnlySpan<byte> content)
            {
                _owner.DispatchFrame(_userTokenId, (byte)ProtocalType.Agree, content);
            }
        }

    }
}
