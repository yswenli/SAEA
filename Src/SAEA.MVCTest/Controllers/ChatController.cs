/****************************************************************************
 *项目名称：SAEA.MVCTest.Controllers
 *CLR 版本：4.0.30319.42000
 *命名空间：SAEA.MVCTest.Controllers
 *类 名 称：ChatController
 *描述：同端口 WebSocket 聊天示例（GET /chat/open?token=xxx）
 *
 *鉴权：不带 token（或 token 为空）时以匿名身份连接，服务端分配 guest-xxxxxxxx；
 *      群组操作要求登录，其它操作（whoami / dm / 回显）匿名也可用。
 *
 *客户端 -> 服务端（文本帧，JSON）：
 *  {"type":"whoami"}
 *  {"type":"group.create","group":"room"}
 *  {"type":"group.join","group":"room"}
 *  {"type":"group.leave","group":"room"}
 *  {"type":"group.send","group":"room","text":"hi"}
 *  {"type":"group.list"}
 *  {"type":"group.members","group":"room"}
 *  {"type":"dm","to":"bob","text":"hi"}
 *
 *服务端 -> 客户端（文本帧，JSON）：
 *  {"type":"welcome","user":"alice"}
 *  {"type":"error","code":"user_offline","message":"..."}
 *  ...
 *
 *兼容：非 JSON 文本原样回显、二进制原样回显、server-close 触发 1011 关闭。
 *=====================================================================
 *****************************************************************************/
using JWT.Net;

using SAEA.Common.Newtonsoft.Json;
using SAEA.Common.Serialization;
using SAEA.Http.Model;
using SAEA.MVC;
using SAEA.MVC.WebSockets;
using SAEA.MVCTest.Model;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 同端口 WebSocket 聊天示例控制器。
    /// 浏览器连接地址为 ws://当前主机/chat/open?token=登录接口返回的 JWT；
    /// 不带 token 时以匿名身份连接，仅群组操作要求登录。
    /// </summary>
    public class ChatController : WSController
    {
        const string JwtPassword = "yswenli";

        const string BearerPrefix = "Bearer ";

        const int MaxGroupNameLength = 64;

        /// <summary>
        /// 聊天状态全部放在静态内存表里，进程重启即清空；
        /// 同一把锁保护三张表，广播前取快照，锁外发送。
        /// </summary>
        static readonly object _sync = new object();

        /// <summary>
        /// userName -> 当前在线连接
        /// </summary>
        static readonly Dictionary<string, IWebSocketConnection> _online =
            new Dictionary<string, IWebSocketConnection>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 连接 Id -> 连接身份，CloseAsync 时用连接 Id 反查用户
        /// </summary>
        static readonly Dictionary<string, ChatUser> _connectionUser =
            new Dictionary<string, ChatUser>(StringComparer.Ordinal);

        /// <summary>
        /// groupName -> 成员 userName 集合
        /// </summary>
        static readonly Dictionary<string, HashSet<string>> _groups =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        #region 生命周期

        /// <summary>
        /// 连接入口：带 token 则校验 JWT（失败写 401 且不升级），
        /// 不带 token 则以匿名身份连接；成功后接受握手、绑定身份、顶掉同用户的旧连接。
        /// </summary>
        /// <returns>连接建立与旧连接顶替流程的异步任务</returns>
        public override async Task OpenAsync()
        {
            var request = HttpContext.Current?.Request;

            // 非 GET / 非升级请求不进入鉴权：保持与普通 [WebSocket] action 一致的 400
            if (request == null
                || string.Compare(request.Method, "GET", StringComparison.OrdinalIgnoreCase) != 0
                || !WebSockets.IsWebSocketRequest)
            {
                await WebSockets.AcceptWebSocketAsync();
                return;
            }

            var token = GetQueryValue("token");

            string userName = null;

            var anonymous = string.IsNullOrWhiteSpace(token);

            if (!anonymous)
            {
                string authError;

                if (!TryAuthenticate(token, out userName, out authError))
                {
                    Console.WriteLine($"ChatController.OpenAsync: 鉴权失败 {authError}");
                    WriteUnauthorized(authError);
                    return;
                }
            }

            var ws = await WebSockets.AcceptWebSocketAsync();

            if (ws == null) return;

            IWebSocketConnection replaced = null;

            lock (_sync)
            {
                // 匿名身份在注册锁内生成，保证不与在线用户重名
                if (anonymous)
                {
                    userName = CreateGuestNameLocked();
                }

                if (_online.TryGetValue(userName, out var existing) && !ReferenceEquals(existing, ws))
                {
                    replaced = existing;
                }

                _online[userName] = ws;

                _connectionUser[ws.Id] = new ChatUser { Name = userName, Anonymous = anonymous };
            }

            if (replaced != null)
            {
                // 同一用户重复连接：新连接顶掉旧连接，群组成员关系按用户名保留
                try
                {
                    await replaced.CloseAsync(WSCloseReason.GoingAway);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ChatController.OpenAsync: 关闭旧连接失败 {ex.Message}");
                }
            }

            await SendJson(ws, new { type = "welcome", user = userName, anonymous });
        }

        /// <summary>
        /// 收到消息：JSON 走聊天协议，其它文本/二进制保持原有回显行为
        /// </summary>
        /// <param name="message">收到的 WebSocket 消息，二进制与文本按不同分支处理</param>
        /// <returns>消息处理完成的异步任务</returns>
        public override async Task MessageAsync(WSMessage message)
        {
            var ws = WebSockets.Current;

            if (ws == null) return;

            // 二进制保持原样回显
            if (!message.IsText)
            {
                await ws.SendAsync(message.Data);
                return;
            }

            var text = message.Text ?? string.Empty;

            // 不像是协议消息：保持旧行为（原样回显 / server-close）
            if (!text.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                if (text == "server-close")
                {
                    await ws.CloseAsync(WSCloseReason.ServerShutdown);
                    return;
                }

                await ws.SendAsync(text);
                return;
            }

            ChatUser chatUser;

            lock (_sync)
            {
                _connectionUser.TryGetValue(ws.Id, out chatUser);
            }

            if (chatUser == null)
            {
                await SendErrorAsync(ws, "not_authenticated", "当前连接尚未完成绑定，请重连");
                return;
            }

            var userName = chatUser.Name;

            ChatRequest request;

            try
            {
                request = SerializeHelper.Deserialize<ChatRequest>(text);
            }
            catch (Exception ex)
            {
                await SendErrorAsync(ws, "bad_request", "JSON 解析失败：" + ex.Message);
                return;
            }

            var type = request?.Type?.Trim();

            if (string.IsNullOrEmpty(type))
            {
                await SendErrorAsync(ws, "bad_request", "缺少 type 字段");
                return;
            }

            // 匿名连接只开放回显 / whoami / 私聊，群组操作要求登录
            if (chatUser.Anonymous && type.StartsWith("group.", StringComparison.OrdinalIgnoreCase))
            {
                await SendErrorAsync(ws, "login_required", "群组操作需要登录，请先获取 token 后再连接");
                return;
            }

            switch (type.ToLowerInvariant())
            {
                case "whoami":
                    await SendJson(ws, new { type = "whoami", user = userName, anonymous = chatUser.Anonymous });
                    return;

                case "group.create":
                    await CreateGroupAsync(ws, userName, request.Group);
                    return;

                case "group.join":
                    await JoinGroupAsync(ws, userName, request.Group);
                    return;

                case "group.leave":
                    await LeaveGroupAsync(ws, userName, request.Group);
                    return;

                case "group.send":
                    await SendGroupMessageAsync(ws, userName, request.Group, request.Text);
                    return;

                case "group.list":
                    await ListGroupsAsync(ws);
                    return;

                case "group.members":
                    await ListGroupMembersAsync(ws, request.Group);
                    return;

                case "dm":
                    await SendDirectMessageAsync(ws, userName, request.To, request.Text);
                    return;

                default:
                    await SendErrorAsync(ws, "unknown_type", "未知的 type：" + type);
                    return;
            }
        }

        /// <summary>
        /// 连接关闭回调：按 ws.Id 确认身份后清理在线表和群成员，并通知群内其他人。
        /// 同一用户被新连接顶替时，旧连接的回调不会误删用户/群关系。
        /// </summary>
        /// <param name="reason">关闭原因，仅用于日志；群成员离开通知固定标记为 disconnected</param>
        /// <returns>清理在线表与群成员并广播通知的异步任务</returns>
        public override async Task CloseAsync(WSCloseReason reason)
        {
            var ws = WebSockets.Current;

            if (ws == null)
            {
                Console.WriteLine($"ChatController.CloseAsync: {reason}");
                return;
            }

            var notices = new List<GroupNotice>();

            ChatUser chatUser;

            string userName = null;

            lock (_sync)
            {
                if (!_connectionUser.TryGetValue(ws.Id, out chatUser))
                {
                    Console.WriteLine($"ChatController.CloseAsync: {reason}, connection={ws.Id}");
                    return;
                }

                _connectionUser.Remove(ws.Id);

                userName = chatUser.Name;

                // 只有当前仍是用户最新连接时才清理，防止顶替时旧回调误踢
                if (!_online.TryGetValue(userName, out var current) || !ReferenceEquals(current, ws))
                {
                    Console.WriteLine($"ChatController.CloseAsync: {reason}, user={userName}, replaced=true");
                    return;
                }

                _online.Remove(userName);

                var emptyGroups = new List<string>();

                foreach (var item in _groups)
                {
                    var members = item.Value;

                    if (!members.Remove(userName)) continue;

                    notices.Add(new GroupNotice()
                    {
                        Group = item.Key,
                        Members = new List<string>(members),
                        Targets = ResolveConnections(members, null)
                    });

                    if (members.Count == 0)
                    {
                        emptyGroups.Add(item.Key);
                    }
                }

                foreach (var name in emptyGroups)
                {
                    _groups.Remove(name);
                }
            }

            Console.WriteLine($"ChatController.CloseAsync: {reason}, user={userName}, groups={notices.Count}");

            foreach (var notice in notices)
            {
                foreach (var target in notice.Targets)
                {
                    await SendJson(target, new
                    {
                        type = "group.member_left",
                        group = notice.Group,
                        user = userName,
                        members = notice.Members,
                        reason = "disconnected"
                    });
                }
            }
        }

        /// <summary>
        /// 连接内异常回调
        /// </summary>
        /// <param name="exception">连接内捕获的异常，仅打印消息</param>
        /// <returns>已完成的异步任务</returns>
        public override Task ErrorAsync(Exception exception)
        {
            Console.WriteLine($"ChatController.ErrorAsync: {exception.Message}");
            return Task.CompletedTask;
        }

        #endregion

        #region 群组

        async Task CreateGroupAsync(IWebSocketConnection ws, string userName, string rawName)
        {
            var groupName = NormalizeGroupName(rawName);

            if (groupName == null)
            {
                await SendErrorAsync(ws, "bad_request", $"群名不能为空且不能超过 {MaxGroupNameLength} 个字符");
                return;
            }

            List<string> members;

            lock (_sync)
            {
                if (_groups.ContainsKey(groupName))
                {
                    members = null;
                }
                else
                {
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { userName };

                    _groups[groupName] = set;

                    members = new List<string>(set);
                }
            }

            if (members == null)
            {
                await SendErrorAsync(ws, "group_exists", "群已存在：" + groupName);
                return;
            }

            await SendJson(ws, new
            {
                type = "group.created",
                group = groupName,
                owner = userName,
                members = members
            });
        }

        async Task JoinGroupAsync(IWebSocketConnection ws, string userName, string rawName)
        {
            var groupName = NormalizeGroupName(rawName);

            if (groupName == null)
            {
                await SendErrorAsync(ws, "bad_request", $"群名不能为空且不能超过 {MaxGroupNameLength} 个字符");
                return;
            }

            List<string> members = null;

            List<IWebSocketConnection> others = null;

            lock (_sync)
            {
                if (!_groups.TryGetValue(groupName, out var set))
                {
                    members = null;
                }
                else
                {
                    set.Add(userName);

                    members = new List<string>(set);

                    others = ResolveConnections(set, userName);
                }
            }

            if (members == null)
            {
                await SendErrorAsync(ws, "group_not_found", "群不存在：" + groupName);
                return;
            }

            await SendJson(ws, new
            {
                type = "group.joined",
                group = groupName,
                user = userName,
                members = members
            });

            foreach (var other in others)
            {
                await SendJson(other, new
                {
                    type = "group.member_joined",
                    group = groupName,
                    user = userName,
                    members = members
                });
            }
        }

        async Task LeaveGroupAsync(IWebSocketConnection ws, string userName, string rawName)
        {
            var groupName = NormalizeGroupName(rawName);

            if (groupName == null)
            {
                await SendErrorAsync(ws, "bad_request", $"群名不能为空且不能超过 {MaxGroupNameLength} 个字符");
                return;
            }

            List<string> members = null;

            List<IWebSocketConnection> others = null;

            string error = null;

            lock (_sync)
            {
                if (!_groups.TryGetValue(groupName, out var set))
                {
                    error = "group_not_found";
                }
                else if (!set.Contains(userName))
                {
                    error = "not_in_group";
                }
                else
                {
                    set.Remove(userName);

                    members = new List<string>(set);

                    others = ResolveConnections(set, null);

                    if (set.Count == 0)
                    {
                        _groups.Remove(groupName);
                    }
                }
            }

            if (error != null)
            {
                await SendErrorAsync(ws, error, error == "group_not_found" ? "群不存在：" + groupName : "你不在群内：" + groupName);
                return;
            }

            await SendJson(ws, new
            {
                type = "group.left",
                group = groupName,
                user = userName,
                members = members
            });

            foreach (var other in others)
            {
                await SendJson(other, new
                {
                    type = "group.member_left",
                    group = groupName,
                    user = userName,
                    members = members,
                    reason = "left"
                });
            }
        }

        async Task SendGroupMessageAsync(IWebSocketConnection ws, string userName, string rawName, string text)
        {
            var groupName = NormalizeGroupName(rawName);

            if (groupName == null)
            {
                await SendErrorAsync(ws, "bad_request", $"群名不能为空且不能超过 {MaxGroupNameLength} 个字符");
                return;
            }

            List<IWebSocketConnection> targets = null;

            string error = null;

            lock (_sync)
            {
                if (!_groups.TryGetValue(groupName, out var set))
                {
                    error = "group_not_found";
                }
                else if (!set.Contains(userName))
                {
                    error = "not_in_group";
                }
                else
                {
                    targets = ResolveConnections(set, null);
                }
            }

            if (error != null)
            {
                await SendErrorAsync(ws, error, error == "group_not_found" ? "群不存在：" + groupName : "请先加入群：" + groupName);
                return;
            }

            var payload = text ?? string.Empty;

            foreach (var target in targets)
            {
                await SendJson(target, new
                {
                    type = "group.message",
                    group = groupName,
                    from = userName,
                    text = payload
                });
            }
        }

        async Task ListGroupsAsync(IWebSocketConnection ws)
        {
            var groups = new List<object>();

            lock (_sync)
            {
                foreach (var item in _groups)
                {
                    groups.Add(new
                    {
                        name = item.Key,
                        members = new List<string>(item.Value)
                    });
                }
            }

            await SendJson(ws, new
            {
                type = "group.list",
                groups = groups
            });
        }

        async Task ListGroupMembersAsync(IWebSocketConnection ws, string rawName)
        {
            var groupName = NormalizeGroupName(rawName);

            if (groupName == null)
            {
                await SendErrorAsync(ws, "bad_request", $"群名不能为空且不能超过 {MaxGroupNameLength} 个字符");
                return;
            }

            List<string> members = null;

            lock (_sync)
            {
                if (_groups.TryGetValue(groupName, out var set))
                {
                    members = new List<string>(set);
                }
            }

            if (members == null)
            {
                await SendErrorAsync(ws, "group_not_found", "群不存在：" + groupName);
                return;
            }

            await SendJson(ws, new
            {
                type = "group.members",
                group = groupName,
                members = members
            });
        }

        #endregion

        #region 私聊

        async Task SendDirectMessageAsync(IWebSocketConnection ws, string from, string rawTo, string text)
        {
            var to = rawTo?.Trim();

            if (string.IsNullOrEmpty(to))
            {
                await SendErrorAsync(ws, "bad_request", "缺少 to 字段");
                return;
            }

            IWebSocketConnection target = null;

            lock (_sync)
            {
                if (_online.TryGetValue(to, out var connection) && connection.State == WebSocketState.Open)
                {
                    target = connection;
                }
            }

            if (target == null)
            {
                // 不做离线消息存储：对方不在线直接报错
                await SendErrorAsync(ws, "user_offline", "用户不在线：" + to);
                return;
            }

            var payload = text ?? string.Empty;

            await SendJson(target, new
            {
                type = "dm",
                from = from,
                to = to,
                text = payload
            });

            await SendJson(ws, new
            {
                type = "dm.sent",
                to = to,
                text = payload
            });
        }

        #endregion

        #region 鉴权与响应

        static string GetQueryValue(string key)
        {
            var query = HttpContext.Current?.Request?.Query;

            if (query == null) return null;

            return query.TryGetValue(key, out var value) ? value : null;
        }

        static bool TryAuthenticate(string token, out string userName, out string error)
        {
            userName = null;

            error = null;

            if (string.IsNullOrWhiteSpace(token))
            {
                error = "缺少 token";
                return false;
            }

            var raw = token.Trim();

            // 登录接口返回的 Authorization 可能带多层 Bearer 前缀，这里全部去掉
            while (raw.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                raw = raw.Substring(BearerPrefix.Length).Trim();
            }

            if (string.IsNullOrEmpty(raw))
            {
                error = "token 为空";
                return false;
            }

            try
            {
                var jwt = JWTPackage<UserInfo>.Parse(raw, JwtPassword);

                var info = jwt?.Payload?.Data;

                if (info == null || string.IsNullOrWhiteSpace(info.UserName))
                {
                    error = "token 中没有有效的用户名";
                    return false;
                }

                userName = info.UserName.Trim();

                return true;
            }
            catch (Exception ex)
            {
                error = "token 校验失败：" + ex.Message;
                return false;
            }
        }

        static void WriteUnauthorized(string message)
        {
            var body = Encoding.UTF8.GetBytes(message ?? "Unauthorized");

            var head = Encoding.UTF8.GetBytes(
                "HTTP/1.1 401 Unauthorized\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                "Content-Length: " + body.Length + "\r\n" +
                "Connection: close\r\n\r\n");

            var all = new byte[head.Length + body.Length];

            Buffer.BlockCopy(head, 0, all, 0, head.Length);

            Buffer.BlockCopy(body, 0, all, head.Length, body.Length);

            HttpContext.Current.Response.WriteRaw(all, true);
        }

        static Task SendJson(IWebSocketConnection ws, object payload)
        {
            if (ws == null) return Task.CompletedTask;

            return ws.SendAsync(SerializeHelper.Serialize(payload));
        }

        static Task SendErrorAsync(IWebSocketConnection ws, string code, string message)
        {
            return SendJson(ws, new
            {
                type = "error",
                code = code,
                message = message
            });
        }

        #endregion

        #region 内部工具

        /// <summary>
        /// 在 _sync 锁内生成唯一的匿名用户名（guest-xxxxxxxx）
        /// </summary>
        static string CreateGuestNameLocked()
        {
            while (true)
            {
                var name = "guest-" + Guid.NewGuid().ToString("N").Substring(0, 8);

                if (!_online.ContainsKey(name)) return name;
            }
        }

        static string NormalizeGroupName(string rawName)
        {
            var name = rawName?.Trim();

            if (string.IsNullOrEmpty(name) || name.Length > MaxGroupNameLength)
            {
                return null;
            }

            return name;
        }

        /// <summary>
        /// 在锁内把成员名解析成在线连接快照，发送在锁外进行
        /// </summary>
        static List<IWebSocketConnection> ResolveConnections(HashSet<string> members, string excludeUser)
        {
            var list = new List<IWebSocketConnection>();

            if (members == null || members.Count == 0) return list;

            foreach (var member in members)
            {
                if (excludeUser != null && string.Equals(member, excludeUser, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_online.TryGetValue(member, out var connection) && connection != null)
                {
                    list.Add(connection);
                }
            }

            return list;
        }

        sealed class ChatUser
        {
            public string Name { get; set; }

            public bool Anonymous { get; set; }
        }

        sealed class ChatRequest
        {
            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("group")]
            public string Group { get; set; }

            [JsonProperty("to")]
            public string To { get; set; }

            [JsonProperty("text")]
            public string Text { get; set; }
        }

        sealed class GroupNotice
        {
            public string Group { get; set; }

            public List<string> Members { get; set; }

            public List<IWebSocketConnection> Targets { get; set; }
        }

        #endregion
    }
}