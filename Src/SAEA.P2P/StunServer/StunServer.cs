/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.P2P.StunServer
*文件名： StunServer
*版本号： v26.11.5.1
*唯一标识：d821f8bf-9b1d-4804-b0de-5eea3d9c2068
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/11 19:00:00
*描述：STUN 服务端——Binding 请求应答、RFC 5780 换地址换端口与未知属性处理
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 19:00:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 STUN 服务端
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SAEA.P2P.Common;
using SAEA.P2P.Stun;

namespace SAEA.P2P.StunServer
{
    /// <summary>
    /// STUN 服务端：在配置的端点集合上应答 Binding 请求，并按 RFC 5780 从换地址或换端口的端点发出响应。
    /// 请求带 FINGERPRINT 时响应也带 FINGERPRINT；indication 静默丢弃；未知必选属性回 420。
    /// </summary>
    public sealed class StunServer : IDisposable
    {
        /// <summary>
        /// CHANGE-REQUEST 的换地址位
        /// </summary>
        private const uint ChangeAddressFlag = 0x04;

        /// <summary>
        /// CHANGE-REQUEST 的换端口位
        /// </summary>
        private const uint ChangePortFlag = 0x02;

        /// <summary>
        /// 服务端配置
        /// </summary>
        private readonly StunServerOptions _options;

        /// <summary>
        /// 保护启动状态与端点列表的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 已绑定的监听端点
        /// </summary>
        private readonly List<StunServerEndpoint> _endpoints = new List<StunServerEndpoint>();

        /// <summary>
        /// 是否正在启动
        /// </summary>
        private bool _starting;

        /// <summary>
        /// 是否已启动完成
        /// </summary>
        private bool _started;

        /// <summary>
        /// 是否已释放
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 创建服务端；调用 <see cref="StartAsync"/> 后开始监听
        /// </summary>
        /// <param name="options">服务端配置</param>
        /// <exception cref="ArgumentNullException">options 为 null</exception>
        public StunServer(StunServerOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;
        }

        /// <summary>
        /// 已绑定的监听端点；启动前为空
        /// </summary>
        public IReadOnlyList<StunServerEndpoint> Endpoints
        {
            get
            {
                lock (_sync)
                {
                    return _endpoints.ToArray();
                }
            }
        }

        /// <summary>
        /// 绑定全部监听端点并启动接收；只能调用一次，失败后已绑定的端点会被释放
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>启动完成的任务</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfDisposed();

            lock (_sync)
            {
                if (_started || _starting)
                {
                    throw new InvalidOperationException("服务端只能启动一次");
                }
                _starting = true;
            }

            var created = new List<StunServerEndpoint>();
            try
            {
                var targets = _options.BuildBindEndPoints();
                for (var i = 0; i < targets.Count; i++)
                {
                    var transport = _options.CreateTransport();
                    StunServerEndpoint endpoint;
                    try
                    {
                        await transport.BindAsync(targets[i], cancellationToken).ConfigureAwait(false);
                        endpoint = new StunServerEndpoint(transport);
                        var captured = endpoint;
                        transport.DatagramReceived += (remoteEndPoint, data) => OnDatagramReceived(captured, remoteEndPoint, data);
                        endpoint.StartReceive();
                    }
                    catch
                    {
                        transport.Dispose();
                        throw;
                    }
                    created.Add(endpoint);
                }

                lock (_sync)
                {
                    _endpoints.AddRange(created);
                    _started = true;
                }
            }
            catch
            {
                for (var i = 0; i < created.Count; i++)
                {
                    created[i].Dispose();
                }
                throw;
            }
            finally
            {
                lock (_sync)
                {
                    _starting = false;
                }
            }
        }

        /// <summary>
        /// 释放全部监听端点；可重复调用
        /// </summary>
        public void Dispose()
        {
            StunServerEndpoint[] snapshot;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                snapshot = _endpoints.ToArray();
                _endpoints.Clear();
            }

            for (var i = 0; i < snapshot.Length; i++)
            {
                snapshot[i].Dispose();
            }
        }

        /// <summary>
        /// 单个监听端点的报文入口：解析后按方法与类别分派
        /// </summary>
        /// <param name="receivedOn">收到报文的监听端点</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="data">数据报内容</param>
        private void OnDatagramReceived(StunServerEndpoint receivedOn, IPEndPoint remoteEndPoint, ReadOnlyMemory<byte> data)
        {
            try
            {
                // TURN 的 ChannelData 不是 STUN 报文，必须在解析前先给出入口
                var datagramHandler = _options.DatagramHandler;
                if (datagramHandler != null && datagramHandler(data, remoteEndPoint, receivedOn))
                {
                    return;
                }

                StunMessage request;
                StunParseError error;
                if (!StunMessage.TryParse(data.Span, out request, out error))
                {
                    return;
                }

                // 指示没有事务，任何情况下都不得回应答或错误；
                // 但 Send 指示必须交给 TURN 处理，因此非 Binding 指示仍要分派一次
                if (request.Class == StunClass.Indication)
                {
                    if (request.Method != StunMessageType.Binding)
                    {
                        DispatchOtherMethod(request, remoteEndPoint, receivedOn, false);
                    }
                    return;
                }

                if (request.Class != StunClass.Request)
                {
                    return;
                }

                if (request.Method != StunMessageType.Binding)
                {
                    DispatchOtherMethod(request, remoteEndPoint, receivedOn, true);
                    return;
                }

                StunAttribute fingerprint;
                if (request.TryGet(StunAttributeType.Fingerprint, out fingerprint) && !StunFingerprint.TryVerify(request))
                {
                    P2PLogHelper.Debug("StunServer", "请求 FINGERPRINT 校验失败，已丢弃");
                    return;
                }

                if (request.HasUnknownComprehensionRequired)
                {
                    var errorResponse = BuildError(request, 420, "Unknown Attribute", request.UnknownComprehensionRequiredTypes);
                    Send(receivedOn, remoteEndPoint, errorResponse);
                    return;
                }

                HandleBinding(request, remoteEndPoint, receivedOn);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("StunServer", "STUN_SERVER_HANDLE", "处理数据报失败", ex);
            }
        }

        /// <summary>
        /// 处理 Binding 请求：解析 CHANGE-REQUEST 决定响应从哪个端点发出
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">收到请求的监听端点</param>
        private void HandleBinding(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn)
        {
            var localEndPoint = receivedOn.LocalEndPoint;
            var sendEndPoint = localEndPoint;

            StunAttribute change;
            if (request.TryGet(StunAttributeType.ChangeRequest, out change))
            {
                uint flags;
                if (!StunAttributeCodec.TryGetUInt32(change, out flags))
                {
                    Send(receivedOn, remoteEndPoint, BuildError(request, 400, "Bad Request", null));
                    return;
                }

                var changeAddress = (flags & ChangeAddressFlag) != 0;
                var changePort = (flags & ChangePortFlag) != 0;

                // 单地址或单端口部署无法满足请求的组合，按 RFC 5780 回 420 并列出 CHANGE-REQUEST
                if ((changeAddress && !_options.CanChangeAddress) || (changePort && !_options.CanChangePort))
                {
                    Send(receivedOn, remoteEndPoint, BuildError(
                        request,
                        420,
                        "Unknown Attribute",
                        new[] { (ushort)StunAttributeType.ChangeRequest }));
                    return;
                }

                sendEndPoint = ResolveChangedEndPoint(localEndPoint, changeAddress, changePort);
            }

            var sender = FindEndpoint(sendEndPoint);
            if (sender == null)
            {
                P2PLogHelper.Warning("StunServer", "找不到响应端点 " + sendEndPoint);
                return;
            }

            var response = BuildSuccess(request, remoteEndPoint, sendEndPoint, GetOtherEndPoint(localEndPoint));
            Send(sender, remoteEndPoint, response);
        }

        /// <summary>
        /// 把非 Binding 请求交给配置的分派点；未配置或返回 null 时不响应
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="remoteEndPoint">客户端来源端点</param>
        /// <param name="receivedOn">收到请求的监听端点</param>
        private void DispatchOtherMethod(StunMessage request, IPEndPoint remoteEndPoint, StunServerEndpoint receivedOn, bool allowResponse)
        {
            var handler = _options.RequestHandler;
            if (handler == null)
            {
                return;
            }

            byte[] response;
            try
            {
                response = handler(request, remoteEndPoint, receivedOn);
            }
            catch (Exception ex)
            {
                P2PLogHelper.Error("StunServer", "STUN_SERVER_DISPATCH", "分派非 Binding 请求失败", ex);
                return;
            }

            // 指示没有事务，处理方即使返回了报文也不得回送
            if (allowResponse && response != null)
            {
                Send(receivedOn, remoteEndPoint, response);
            }
        }

        /// <summary>
        /// 按换地址与换端口标志算出应当使用的本地端点
        /// </summary>
        /// <param name="localEndPoint">收到请求的本地端点</param>
        /// <param name="changeAddress">是否换地址</param>
        /// <param name="changePort">是否换端口</param>
        /// <returns>响应端点</returns>
        private IPEndPoint ResolveChangedEndPoint(IPEndPoint localEndPoint, bool changeAddress, bool changePort)
        {
            var primary = _options.PrimaryEndPoint;
            var alternate = _options.AlternateEndPoint;

            var address = localEndPoint.Address;
            if (changeAddress)
            {
                address = localEndPoint.Address.Equals(primary.Address) ? alternate.Address : primary.Address;
            }

            var port = localEndPoint.Port;
            if (changePort)
            {
                port = localEndPoint.Port == primary.Port ? alternate.Port : primary.Port;
            }

            return new IPEndPoint(address, port);
        }

        /// <summary>
        /// 计算相对当前端点而言的备用端点
        /// </summary>
        /// <param name="localEndPoint">收到请求的本地端点</param>
        /// <returns>备用端点；未配置时为 null</returns>
        private IPEndPoint GetOtherEndPoint(IPEndPoint localEndPoint)
        {
            if (!_options.HasAlternate)
            {
                return null;
            }

            var primary = _options.PrimaryEndPoint;
            var alternate = _options.AlternateEndPoint;
            var address = localEndPoint.Address.Equals(primary.Address) ? alternate.Address : primary.Address;
            var port = localEndPoint.Port == primary.Port ? alternate.Port : primary.Port;
            return new IPEndPoint(address, port);
        }

        /// <summary>
        /// 按本地端点查找已绑定的监听端点
        /// </summary>
        /// <param name="localEndPoint">目标本地端点</param>
        /// <returns>匹配的端点；不存在时为 null</returns>
        private StunServerEndpoint FindEndpoint(IPEndPoint localEndPoint)
        {
            lock (_sync)
            {
                for (var i = 0; i < _endpoints.Count; i++)
                {
                    var candidate = _endpoints[i].LocalEndPoint;
                    if (candidate != null
                        && candidate.Port == localEndPoint.Port
                        && candidate.Address.Equals(localEndPoint.Address))
                    {
                        return _endpoints[i];
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 构造成功应答
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="mappedEndPoint">客户端映射地址</param>
        /// <param name="responseOrigin">响应来源端点</param>
        /// <param name="otherAddress">备用地址；未配置时为 null</param>
        /// <returns>应答字节</returns>
        private byte[] BuildSuccess(StunMessage request, IPEndPoint mappedEndPoint, IPEndPoint responseOrigin, IPEndPoint otherAddress)
        {
            var transactionId = request.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.SuccessResponse, transactionId);
            var attributes = new List<StunAttribute>
            {
                StunAddressCodec.EncodeAddress(StunAttributeType.XorMappedAddress, mappedEndPoint, true, transactionId),
                StunAddressCodec.EncodeAddress(StunAttributeType.MappedAddress, mappedEndPoint, false),
                StunAddressCodec.EncodeAddress(StunAttributeType.ResponseOrigin, responseOrigin, false)
            };

            if (otherAddress != null)
            {
                attributes.Add(StunAddressCodec.EncodeAddress(StunAttributeType.OtherAddress, otherAddress, false));
            }

            AddSoftware(attributes);
            response.SetAttributes(attributes);
            AppendFingerprintIfRequested(request, response);
            return response.ToArray();
        }

        /// <summary>
        /// 构造错误应答
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="code">STUN 错误码</param>
        /// <param name="reason">原因短语</param>
        /// <param name="unknownAttributes">未知属性列表；为 null 或空时不携带</param>
        /// <returns>应答字节</returns>
        private byte[] BuildError(StunMessage request, int code, string reason, IEnumerable<ushort> unknownAttributes)
        {
            var transactionId = request.TransactionId.ToArray();
            var response = new StunMessage(StunMessageType.Binding, StunClass.ErrorResponse, transactionId);
            var attributes = new List<StunAttribute>
            {
                StunErrorCode.Create(code, reason)
            };

            if (unknownAttributes != null)
            {
                var value = new List<byte>();
                foreach (var type in unknownAttributes)
                {
                    value.Add((byte)(type >> 8));
                    value.Add((byte)type);
                }

                if (value.Count > 0)
                {
                    attributes.Add(new StunAttribute(StunAttributeType.UnknownAttributes, value.ToArray()));
                }
            }

            AddSoftware(attributes);
            response.SetAttributes(attributes);
            AppendFingerprintIfRequested(request, response);
            return response.ToArray();
        }

        /// <summary>
        /// 追加 SOFTWARE 属性；未配置时跳过
        /// </summary>
        /// <param name="attributes">待发送属性列表</param>
        private void AddSoftware(List<StunAttribute> attributes)
        {
            if (!string.IsNullOrEmpty(_options.Software))
            {
                attributes.Add(new StunAttribute(StunAttributeType.Software, Encoding.UTF8.GetBytes(_options.Software)));
            }
        }

        /// <summary>
        /// 请求带 FINGERPRINT 时给应答也追加 FINGERPRINT
        /// </summary>
        /// <param name="request">请求</param>
        /// <param name="response">应答</param>
        private static void AppendFingerprintIfRequested(StunMessage request, StunMessage response)
        {
            StunAttribute fingerprint;
            if (request.TryGet(StunAttributeType.Fingerprint, out fingerprint))
            {
                StunFingerprint.Append(response);
            }
        }

        /// <summary>
        /// 发送应答；发送失败只记录日志，不能影响接收循环
        /// </summary>
        /// <param name="endpoint">发送端点</param>
        /// <param name="remoteEndPoint">目标端点</param>
        /// <param name="data">应答字节</param>
        private static void Send(StunServerEndpoint endpoint, IPEndPoint remoteEndPoint, byte[] data)
        {
            try
            {
                var task = endpoint.SendAsync(remoteEndPoint, data);
                if (task.IsFaulted)
                {
                    var ignored = task.Exception;
                    P2PLogHelper.Debug("StunServer", "发送应答失败：" + ignored.GetBaseException().Message);
                    return;
                }

                if (!task.IsCompleted)
                {
                    task.ContinueWith(
                        t =>
                        {
                            var ignored = t.Exception;
                            P2PLogHelper.Debug("StunServer", "发送应答失败：" + ignored.GetBaseException().Message);
                        },
                        TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch (Exception ex)
            {
                P2PLogHelper.Debug("StunServer", "发送应答失败：" + ex.Message);
            }
        }

        /// <summary>
        /// 已释放时拒绝启动
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
}