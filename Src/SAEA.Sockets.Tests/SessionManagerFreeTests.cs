/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.Sockets.Tests
*文件名： SessionManagerFreeTests
*版本号： net10.0
*唯一标识：8efea6cf-9bda-462b-b673-0c5dccf286a1
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/20 20:52:03
*描述：令牌绑定代次与按代次释放的用例
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 20:52:03
*修改人： yswenli
*版本号： net10.0
*描述：令牌绑定代次与按代次释放的用例
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using SAEA.Sockets.Base;
using SAEA.Sockets.Core;
using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Tests
{
    /// <summary>
    /// 会话管理器释放用例：验证上一代连接遗留的释放调用不会清空并关闭已被复用的令牌
    /// </summary>
    [TestClass]
    public class SessionManagerFreeTests
    {
        const int BufferSize = 1024;

        TcpListener _listener;

        SessionManager _sessionManager;

        readonly List<TcpClient> _clients = new List<TcpClient>();

        readonly List<Socket> _accepted = new List<Socket>();

        /// <summary>
        /// 启动回环监听并创建池容量为 1 的会话管理器，保证复用必然命中同一个令牌对象
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _sessionManager = new SessionManager(new BaseContext<BaseCoder>(), BufferSize, 1, (s, e) => { }, TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// 释放监听、客户端与服务端套接字
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            foreach (var socket in _accepted)
            {
                try { socket.Dispose(); } catch { }
            }
            foreach (var client in _clients)
            {
                try { client.Dispose(); } catch { }
            }
            if (_listener != null) _listener.Stop();
            if (_sessionManager != null) _sessionManager.Clear();
        }

        /// <summary>
        /// 建立一条回环 TCP 连接并返回服务端套接字，远端端点即会话标识来源
        /// </summary>
        /// <returns>已连接的服务端套接字</returns>
        Socket Accept()
        {
            var client = new TcpClient();
            client.Connect(IPAddress.Loopback, ((IPEndPoint)_listener.LocalEndpoint).Port);
            _clients.Add(client);

            var server = _listener.AcceptSocket();
            _accepted.Add(server);
            return server;
        }

        /// <summary>
        /// 令牌已复用给下一代连接后，用旧代次释放必须被拒绝且不改动新连接状态
        /// </summary>
        [TestMethod]
        public void Free_GenerationMismatch_ReturnsFalseAndKeepsSessionId()
        {
            var first = Accept();
            var token = _sessionManager.BindUserToken(first, 1000);

            Assert.IsNotNull(token);
            var oldGeneration = ((BaseUserToken)token).Generation;

            Assert.IsTrue(_sessionManager.Free(token));
            Assert.IsNull(token.ID, "首次释放应清空会话标识");

            var second = Accept();
            var reused = _sessionManager.BindUserToken(second, 1000);

            Assert.AreSame(token, reused, "池容量为 1 时第二次绑定应复用到同一令牌对象");

            var newSessionId = second.RemoteEndPoint.ToString();

            Assert.AreEqual(newSessionId, reused.ID);
            Assert.IsTrue(((BaseUserToken)reused).Generation > oldGeneration, "复用绑定必须推进代次");

            Assert.IsFalse(_sessionManager.Free(reused, oldGeneration), "上一代遗留的释放必须被拒绝");

            Assert.AreEqual(newSessionId, reused.ID, "被拒绝的释放不得清空新一代的会话标识");
            Assert.AreSame(second, reused.Socket, "被拒绝的释放不得替换新一代的套接字");
            Assert.IsTrue(reused.Socket.Connected, "被拒绝的释放不得关闭新一代的套接字");
            Assert.AreSame(reused, _sessionManager.Get(newSessionId), "被拒绝的释放不得摘除新一代的会话缓存");
        }

        /// <summary>
        /// 代次仍是当前值时正常释放，会话标识被清空且会话缓存不再命中
        /// </summary>
        [TestMethod]
        public void Free_CurrentGeneration_ClearsSessionId()
        {
            var socket = Accept();
            var token = _sessionManager.BindUserToken(socket, 1000);

            Assert.IsNotNull(token);
            var sessionId = token.ID;

            Assert.IsFalse(string.IsNullOrEmpty(sessionId));

            Assert.IsTrue(_sessionManager.Free(token, ((BaseUserToken)token).Generation));
            Assert.IsNull(token.ID, "正常释放应清空会话标识");
            Assert.IsNull(_sessionManager.Get(sessionId), "正常释放应摘除会话缓存");
        }

        /// <summary>
        /// 同一令牌第二次绑定后，代次必须严格大于第一次，上一代的释放才有可判别的依据
        /// </summary>
        [TestMethod]
        public void BindUserToken_SecondBind_AdvancesGeneration()
        {
            var first = Accept();
            var token = _sessionManager.BindUserToken(first, 1000);

            Assert.IsNotNull(token);
            var firstGeneration = ((BaseUserToken)token).Generation;

            Assert.IsTrue(_sessionManager.Free(token));

            var second = Accept();
            var reused = _sessionManager.BindUserToken(second, 1000);

            Assert.AreSame(token, reused);
            Assert.IsTrue(((BaseUserToken)reused).Generation > firstGeneration,
                $"第二次绑定的代次应大于第一次：{firstGeneration}");
        }
    }
}