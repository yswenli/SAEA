using SAEA.Audio.Model;
using SAEA.Common.Serialization;
using SAEA.Sockets;
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Audio.Net
{
    public class SocketServer
    {
        IServerSocket _udpServer;

        ConcurrentDictionary<string, IUserToken> _cache;

        ConcurrentDictionary<string, string> _userChannelMapping;

        ConcurrentDictionary<string, string> _invitedMapping;

        ConcurrentDictionary<string, HashSet<string>> _channelUserMapping;

        public SocketServer(IPEndPoint endPoint)
        {
            _cache = new ConcurrentDictionary<string, IUserToken>();

            _userChannelMapping = new ConcurrentDictionary<string, string>();

            _invitedMapping = new ConcurrentDictionary<string, string>();

            _channelUserMapping = new ConcurrentDictionary<string, HashSet<string>>();

            _udpServer = SocketFactory.CreateServerSocket(SocketOptionBuilder.Instance.SetSocket(SAEASocketType.Udp)
                .SetIPEndPoint(endPoint)
                .UseIocp<BaseContext>()
                .SetReadBufferSize(SocketOption.UDPMaxLength)
                .SetWriteBufferSize(SocketOption.UDPMaxLength)
                .SetTimeOut(5000)
                .Build());
            _udpServer.OnAccepted += _udpServer_OnAccepted;
            _udpServer.OnDisconnected += _udpServer_OnDisconnected;
            _udpServer.OnReceive += _udpServer_OnReceive;
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
                        ReplyAgree(userToken.ID, p.Content);
                        break;
                    case ProtocalType.Disagree:
                        ReplyDidagree(userToken.ID, p.Content);
                        break;
                    case ProtocalType.Join:
                        ReplyJoin(userToken.ID, p.Content);
                        break;
                    case ProtocalType.Data:
                        ReplyData(p.Content);
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

            var channelID = _userChannelMapping.GetOrAdd(userID, (u) => Guid.NewGuid().ToString("N"));

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
        /// <param name="data"></param>
        void ReplyAgree(string userID, byte[] data)
        {
            var at = SerializeHelper.Deserialize<AgreeInfo>(Encoding.UTF8.GetString(data));

            var id = at.ID;

            at.ID = userID;

            BaseReply(id, ProtocalType.Invite, Encoding.UTF8.GetBytes(SerializeHelper.Serialize(at)));

            _userChannelMapping.AddOrUpdate(userID, at.ChannelID, (k, v) => at.ChannelID);

            _invitedMapping.TryRemove(userID, out string _);

            _channelUserMapping.AddOrUpdate(at.ChannelID, new HashSet<string>() { id, userID }, (k, v) =>
            {
                if (v == null)
                {
                    v = new HashSet<string>();
                }
                v.Add(id);
                v.Add(userID);
                return v;
            });
        }

        /// <summary>
        /// 处理拒绝
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="data"></param>
        void ReplyDidagree(string userID, byte[] data)
        {
            var channelID = Encoding.UTF8.GetString(data);

            if (_invitedMapping.TryRemove(userID, out string id))
            {
                BaseReply(id, ProtocalType.Disagree, Encoding.UTF8.GetBytes(userID));
            }
        }


        void ReplyJoin(string userID, byte[] data)
        {

        }

        void ReplyData(byte[] data)
        {
            var dt = SAEASerialize.Deserialize<DataInfo>(data);

            if (_channelUserMapping.TryGetValue(dt.ChannelID, out HashSet<string> hs))
            {
                if (hs != null)
                {
                    Parallel.ForEach(hs, (id) =>
                    {
                        BaseReply(id, ProtocalType.Data, dt.Data);
                    });
                }
            }
        }


        private void _udpServer_OnDisconnected(string ID, Exception ex)
        {
            _cache.TryRemove(ID, out IUserToken _);
        }

    }
}
