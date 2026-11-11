/*******
* 此代码为SAEA.RPC.Generater生成
* SAEA.RPC.Generater Developed by yswenli
* 尽量不要修改此代码 2026-11-11 10:02:33
*******/

using System;
using System.Collections.Generic;
using SAEA.Common;
using SAEA.RPC.Consumer;
using SAEA.RPC.Model;
using SAEA.Sockets.Pipeline;
using SAEA.RPCTest.Consumer.Model;
using SAEA.RPCTest.Consumer.Service;

namespace SAEA.RPCTest.Consumer
{
    /// <summary>
    /// RPC服务代理类
    /// </summary>
    public class RPCServiceProxy
    {
        /// <summary>
        /// 处理器链装配回调：每条连接在 RPC 解码之后、异常收口之前调用一次，
        /// 使用者在此追加自己的入站处理器，可观察应答、推送通知与链上异常
        /// </summary>
        public Action<string, IChannelPipeline> ConfigurePipeline
        {
            get { return _serviceConsumer.ConfigurePipeline; }
            set { _serviceConsumer.ConfigurePipeline = value; }
        }

        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 获取连接状态
        /// </summary>
        public bool IsConnected
        {
            get { return _serviceConsumer.IsConnected; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="uri">服务地址</param>
        public RPCServiceProxy(string uri = "rpc://127.0.0.1:39654") : this(uri, 4, 5, 10 * 1000) { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="uri">服务地址</param>
        /// <param name="links">连接数</param>
        /// <param name="retry">重试次数</param>
        /// <param name="timeOut">超时时间</param>
        /// <param name="configurePipeline">处理器链装配回调，在连接建立前写下，建链时生效</param>
        public RPCServiceProxy(string uri, int links = 4, int retry = 5, int timeOut = 10 * 1000, Action<string, IChannelPipeline> configurePipeline = null)
        {
            _serviceConsumer = new ServiceConsumer(new Uri(uri), links, retry, timeOut, configurePipeline);
            _Di = new DicService(_serviceConsumer);
            _Ge = new GenericService(_serviceConsumer);
            _En = new EnumService(_serviceConsumer);
            _Gr = new GroupService(_serviceConsumer);
            _He = new HelloService(_serviceConsumer);
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <returns>重连成功返回 true</returns>
        public bool Reconnect()
        {
            return _serviceConsumer.Reconnect();
        }

        DicService _Di;

        /// <summary>
        /// 字典服务
        /// </summary>
        public DicService DicService
        {
             get{ return _Di; }
        }

        GenericService _Ge;

        /// <summary>
        /// 泛型服务
        /// </summary>
        public GenericService GenericService
        {
             get{ return _Ge; }
        }

        EnumService _En;

        /// <summary>
        /// 枚举服务
        /// </summary>
        public EnumService EnumService
        {
             get{ return _En; }
        }

        GroupService _Gr;

        /// <summary>
        /// 组服务
        /// </summary>
        public GroupService GroupService
        {
             get{ return _Gr; }
        }

        HelloService _He;

        /// <summary>
        /// Hello服务
        /// </summary>
        public HelloService HelloService
        {
             get{ return _He; }
        }

        /// <summary>
        /// 注册接收通知；注册后服务端推送会以 <see cref="SAEA.RPC.Net.RSocketMsg"/> 形式进入处理器链
        /// </summary>
        public void RegistReceiveNotice()
        {
            _serviceConsumer.RegistReceiveNotice();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _serviceConsumer.Dispose();
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// 字典服务的客户端代理，转发字典参数的远程调用
    /// </summary>
    public class DicService
    {
        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceConsumer">服务消费者</param>
        public DicService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }

        /// <summary>
        /// 调用服务端的字典回传方法
        /// </summary>
        /// <param name="id">用户 ID</param>
        /// <param name="data">用户字典数据</param>
        /// <returns>服务端返回的用户字典数据</returns>
        public Dictionary<Int32,UserInfo> Test(Int32 id, Dictionary<Int32,UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<Dictionary<Int32,UserInfo>>("DicService", "Test", id, data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// 泛型服务的客户端代理，转发泛型包装与列表的远程调用
    /// </summary>
    public class GenericService
    {
        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceConsumer">服务消费者</param>
        public GenericService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }

        /// <summary>
        /// 调用服务端的结果包装回传方法
        /// </summary>
        /// <param name="data">待回传的结果包装</param>
        /// <returns>服务端返回的结果包装</returns>
        public ActionResult<UserInfo> Get(ActionResult<UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<ActionResult<UserInfo>>("GenericService", "Get", data);
        }

        /// <summary>
        /// 调用服务端的字符串列表生成方法
        /// </summary>
        /// <returns>服务端返回的字符串列表</returns>
        public List<String> GetListString()
        {
            return _serviceConsumer.RemoteCall<List<String>>("GenericService", "GetListString");
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// 枚举服务的客户端代理，转发枚举参数的远程调用
    /// </summary>
    public class EnumService
    {
        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceConsumer">服务消费者</param>
        public EnumService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }

        /// <summary>
        /// 获取枚举值
        /// </summary>
        /// <param name="est">枚举服务类型</param>
        /// <returns>返回枚举值</returns>
        public ReturnEnum GetEnum(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
        }

        /// <summary>
        /// 异步获取枚举值；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="est">枚举服务类型</param>
        /// <returns>返回枚举值</returns>
        public ReturnEnum GetEnumAsync(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// 组服务的客户端代理，转发组信息相关的远程调用
    /// </summary>
    public class GroupService
    {
        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceConsumer">服务消费者</param>
        public GroupService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }

        /// <summary>
        /// 更新用户列表
        /// </summary>
        /// <param name="users">用户列表</param>
        /// <returns>更新后的用户列表</returns>
        public List<UserInfo> Update(List<UserInfo> users)
        {
            return _serviceConsumer.RemoteCall<List<UserInfo>>("GroupService", "Update", users);
        }

        /// <summary>
        /// 添加组
        /// </summary>
        /// <param name="groupName">组名</param>
        /// <param name="user">用户信息</param>
        /// <returns>组信息</returns>
        public GroupInfo Add(String groupName, UserInfo user)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "Add", groupName, user);
        }

        /// <summary>
        /// 获取组信息
        /// </summary>
        /// <param name="id">组ID</param>
        /// <returns>组信息</returns>
        public GroupInfo GetGroupInfo(Int32 id)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "GetGroupInfo", id);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// Hello 服务的客户端代理，把调用转发到服务端同名方法
    /// </summary>
    public class HelloService
    {
        ServiceConsumer _serviceConsumer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serviceConsumer">服务消费者</param>
        public HelloService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }

        /// <summary>
        /// 调用服务端的 Hello 方法
        /// </summary>
        /// <returns>服务端拼接后的字符串</returns>
        public String Hello()
        {
            return _serviceConsumer.RemoteCall<String>("HelloService", "Hello");
        }

        /// <summary>
        /// 调用服务端的加法方法
        /// </summary>
        /// <param name="x">被加数</param>
        /// <param name="y">加数</param>
        /// <returns>两数之和</returns>
        public Int32 Plus(Int32 x, Int32 y)
        {
            return _serviceConsumer.RemoteCall<Int32>("HelloService", "Plus", x, y);
        }

        /// <summary>
        /// 调用服务端的用户信息回传方法
        /// </summary>
        /// <param name="info">待回传的用户信息</param>
        /// <returns>服务端返回的用户信息</returns>
        public UserInfo Update(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }

        /// <summary>
        /// 更新用户信息；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="info">待更新的用户信息</param>
        /// <returns>服务端返回的用户信息</returns>
        public UserInfo UpdateAsync(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }

        /// <summary>
        /// 调用服务端的字节数据处理方法
        /// </summary>
        /// <param name="data">待发送的字节数据</param>
        /// <returns>服务端处理后的字节数据</returns>
        public Byte[] SendData(Byte[] data)
        {
            return _serviceConsumer.RemoteCall<Byte[]>("HelloService", "SendData", data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// 用户信息模型，与服务端定义保持一致以保证序列化兼容
    /// </summary>
    [Serializable]
    public class UserInfo
    {
        /// <summary>
        /// 用户唯一标识
        /// </summary>
        public Int32 ID
        {
            get;set;
        }

        /// <summary>
        /// 用户名
        /// </summary>
        public String UserName
        {
            get;set;
        }

        /// <summary>
        /// 出生日期
        /// </summary>
        public DateTime Birthday
        {
            get;set;
        }

        /// <summary>
        /// 用户积分
        /// </summary>
        public Decimal Score
        {
            get;set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// 统一返回结果包装，与服务端定义保持一致以保证序列化兼容
    /// </summary>
    /// <typeparam name="T">业务数据类型</typeparam>
    public class ActionResult<T>
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public Boolean Success
        {
            get;set;
        }

        /// <summary>
        /// 失败时的错误描述，成功时为 null
        /// </summary>
        public String Error
        {
            get;set;
        }

        /// <summary>
        /// 结果状态码
        /// </summary>
        public Int32 Code
        {
            get;set;
        }

        /// <summary>
        /// 业务数据
        /// </summary>
        public T Data
        {
            get;set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// 服务返回档位枚举，与服务端定义保持一致以保证反序列化兼容
    /// </summary>
    public enum ReturnEnum:Int32
    {
        /// <summary>
        /// 小档位，值为 0
        /// </summary>
        Big=0,
        /// <summary>
        /// 中档位，值为 1
        /// </summary>
        Bigger=1,
        /// <summary>
        /// 大档位，值为 2
        /// </summary>
        Biggest=2,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// 服务评价等级枚举，与服务端定义保持一致以保证反序列化兼容
    /// </summary>
    public enum EnumServiceType:Int32
    {
        /// <summary>
        /// 初级评价，值为 1
        /// </summary>
        Good=1,
        /// <summary>
        /// 中级评价，值为 2
        /// </summary>
        Better=2,
        /// <summary>
        /// 高级评价，值为 3
        /// </summary>
        Best=3,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// 组信息模型，与服务端定义保持一致以保证序列化兼容
    /// </summary>
    [Serializable]
    public class GroupInfo
    {
        /// <summary>
        /// 组唯一标识
        /// </summary>
        public Int32 GroupID
        {
            get;set;
        }

        /// <summary>
        /// 组名称
        /// </summary>
        public String Name
        {
            get;set;
        }

        /// <summary>
        /// 是否为临时组
        /// </summary>
        public Boolean IsTemporary
        {
            get;set;
        }

        /// <summary>
        /// 组的创建时间
        /// </summary>
        public DateTime Created
        {
            get;set;
        }

        /// <summary>
        /// 组的创建者
        /// </summary>
        public UserInfo Creator
        {
            get;set;
        }

        /// <summary>
        /// 组的成员列表
        /// </summary>
        public List<UserInfo> Users
        {
            get;set;
        }

        /// <summary>
        /// 按用户 ID 索引的限制名单
        /// </summary>
        public Dictionary<Int32,UserInfo> Limit
        {
            get;set;
        }
    }
}