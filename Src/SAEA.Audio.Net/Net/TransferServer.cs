/****************************************************************************
*项目名称：SAEA.Audio
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Net
*类 名 称：TransferServer
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
    public class TransferServer<T> where T : IStorage
    {
        IServerSocket _udpServer;

        ConcurrentDictionary<string, IUserToken> _cache;


        ConcurrentDictionary<string, string> _invitedMapping;

        IStorage _storage;

        public event Action<DataInfo> OnReceive;

        public TransferServer(IPEndPoint endPoint)
        {
            _cache = new ConcurrentDictionary<string, IUserToken>();

            _invitedMapping = new ConcurrentDictionary<string, string>();

            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UseIocp<BaseContext>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetTimeOut(60 * 1000)
                .Build());
            _udpServer.OnAccepted += _udpServer_OnAccepted;
            _udpServer.OnDisconnected += _udpServer_OnDisconnected;
            _udpServer.OnReceive += _udpServer_OnReceive;

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


        private void _udpServer_OnReceive(ISession currentSession, byte[] data)
        {
            var userToken = (IUserToken)currentSession;

            userToken.Unpacker.Unpack(data, (p) =>
            {
                var protocalType = (ProtocalType)p.Type;

                switch (protocalType)
                {
                    case ProtocalType.Ping:
                        BaseReply(userToken.ID, ProtocalType.Pong, null);
                        break;
                    case ProtocalType.Pong:

                        break;
                    case ProtocalType.Invite:
                        ReplyInvite(userToken.ID, p.Content);
                        break;
                    case ProtocalType.Agree:
                        ReplyAgree(userToken.ID);
                        break;
                    case ProtocalType.Disagree:
                        ReplyDidagree(userToken.ID);
                        break;
                    case ProtocalType.Join:
                        ReplyJoin(userToken.ID, p.Content);
                        break;
                    case ProtocalType.Data:
                        ReplyData(p.Content);
                        break;
                    case ProtocalType.Quit:
                        ReplyQuit(userToken.ID);
                        break;
                }
            });
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
            _udpServer.SendAsync(id, p.ToBytes());
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

    }
}
