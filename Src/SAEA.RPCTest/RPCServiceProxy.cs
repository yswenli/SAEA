/*******
* 此代码为SAEA.RPC.Generater生成
* SAEA.RPC.Generater Developed by yswenli
* 尽量不要修改此代码 2026-11-11 10:54:01
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
        /// 使用者在此追加自己的入站处理器，可观察应答、推送通知与链上异常。
        /// 代理在构造期建立连接，请在构造函数中注入，构造完成后再赋值会抛 InvalidOperationException
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
        /// DicService 服务客户端代理
        /// </summary>
        public DicService DicService
        {
             get{ return _Di; }
        }

        GenericService _Ge;

        /// <summary>
        /// GenericService 服务客户端代理
        /// </summary>
        public GenericService GenericService
        {
             get{ return _Ge; }
        }

        EnumService _En;

        /// <summary>
        /// EnumService 服务客户端代理
        /// </summary>
        public EnumService EnumService
        {
             get{ return _En; }
        }

        GroupService _Gr;

        /// <summary>
        /// GroupService 服务客户端代理
        /// </summary>
        public GroupService GroupService
        {
             get{ return _Gr; }
        }

        HelloService _He;

        /// <summary>
        /// HelloService 服务客户端代理
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
    /// DicService 的客户端代理
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
        /// 调用服务端 Test 方法
        /// </summary>
        /// <param name="id">参数 id</param>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public Dictionary<Int32,UserInfo> Test(Int32 id, Dictionary<Int32,UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<Dictionary<Int32,UserInfo>>("DicService", "Test", id, data);
        }

        /// <summary>
        /// 异步调用服务端 Test 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="id">参数 id</param>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public Dictionary<Int32,UserInfo> TestAsync(Int32 id, Dictionary<Int32,UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<Dictionary<Int32,UserInfo>>("DicService", "Test", id, data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// GenericService 的客户端代理
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
        /// 调用服务端 Get 方法
        /// </summary>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public ActionResult<UserInfo> Get(ActionResult<UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<ActionResult<UserInfo>>("GenericService", "Get", data);
        }

        /// <summary>
        /// 异步调用服务端 Get 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public ActionResult<UserInfo> GetAsync(ActionResult<UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<ActionResult<UserInfo>>("GenericService", "Get", data);
        }

        /// <summary>
        /// 调用服务端 GetListString 方法
        /// </summary>
        /// <returns>远程调用结果</returns>
        public List<String> GetListString()
        {
            return _serviceConsumer.RemoteCall<List<String>>("GenericService", "GetListString");
        }

        /// <summary>
        /// 异步调用服务端 GetListString 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <returns>远程调用结果</returns>
        public List<String> GetListStringAsync()
        {
            return _serviceConsumer.RemoteCall<List<String>>("GenericService", "GetListString");
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// EnumService 的客户端代理
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
        /// 调用服务端 GetEnum 方法
        /// </summary>
        /// <param name="est">参数 est</param>
        /// <returns>远程调用结果</returns>
        public ReturnEnum GetEnum(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
        }

        /// <summary>
        /// 异步调用服务端 GetEnum 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="est">参数 est</param>
        /// <returns>远程调用结果</returns>
        public ReturnEnum GetEnumAsync(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// GroupService 的客户端代理
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
        /// 调用服务端 Update 方法
        /// </summary>
        /// <param name="users">参数 users</param>
        /// <returns>远程调用结果</returns>
        public List<UserInfo> Update(List<UserInfo> users)
        {
            return _serviceConsumer.RemoteCall<List<UserInfo>>("GroupService", "Update", users);
        }

        /// <summary>
        /// 异步调用服务端 Update 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="users">参数 users</param>
        /// <returns>远程调用结果</returns>
        public List<UserInfo> UpdateAsync(List<UserInfo> users)
        {
            return _serviceConsumer.RemoteCall<List<UserInfo>>("GroupService", "Update", users);
        }

        /// <summary>
        /// 调用服务端 Add 方法
        /// </summary>
        /// <param name="groupName">参数 groupName</param>
        /// <param name="user">参数 user</param>
        /// <returns>远程调用结果</returns>
        public GroupInfo Add(String groupName, UserInfo user)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "Add", groupName, user);
        }

        /// <summary>
        /// 异步调用服务端 Add 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="groupName">参数 groupName</param>
        /// <param name="user">参数 user</param>
        /// <returns>远程调用结果</returns>
        public GroupInfo AddAsync(String groupName, UserInfo user)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "Add", groupName, user);
        }

        /// <summary>
        /// 调用服务端 GetGroupInfo 方法
        /// </summary>
        /// <param name="id">参数 id</param>
        /// <returns>远程调用结果</returns>
        public GroupInfo GetGroupInfo(Int32 id)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "GetGroupInfo", id);
        }

        /// <summary>
        /// 异步调用服务端 GetGroupInfo 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="id">参数 id</param>
        /// <returns>远程调用结果</returns>
        public GroupInfo GetGroupInfoAsync(Int32 id)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "GetGroupInfo", id);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    /// <summary>
    /// HelloService 的客户端代理
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
        /// 调用服务端 Hello 方法
        /// </summary>
        /// <returns>远程调用结果</returns>
        public String Hello()
        {
            return _serviceConsumer.RemoteCall<String>("HelloService", "Hello");
        }

        /// <summary>
        /// 异步调用服务端 Hello 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <returns>远程调用结果</returns>
        public String HelloAsync()
        {
            return _serviceConsumer.RemoteCall<String>("HelloService", "Hello");
        }

        /// <summary>
        /// 调用服务端 Plus 方法
        /// </summary>
        /// <param name="x">参数 x</param>
        /// <param name="y">参数 y</param>
        /// <returns>远程调用结果</returns>
        public Int32 Plus(Int32 x, Int32 y)
        {
            return _serviceConsumer.RemoteCall<Int32>("HelloService", "Plus", x, y);
        }

        /// <summary>
        /// 异步调用服务端 Plus 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="x">参数 x</param>
        /// <param name="y">参数 y</param>
        /// <returns>远程调用结果</returns>
        public Int32 PlusAsync(Int32 x, Int32 y)
        {
            return _serviceConsumer.RemoteCall<Int32>("HelloService", "Plus", x, y);
        }

        /// <summary>
        /// 调用服务端 Update 方法
        /// </summary>
        /// <param name="info">参数 info</param>
        /// <returns>远程调用结果</returns>
        public UserInfo Update(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }

        /// <summary>
        /// 异步调用服务端 Update 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="info">参数 info</param>
        /// <returns>远程调用结果</returns>
        public UserInfo UpdateAsync(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }

        /// <summary>
        /// 调用服务端 SendData 方法
        /// </summary>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public Byte[] SendData(Byte[] data)
        {
            return _serviceConsumer.RemoteCall<Byte[]>("HelloService", "SendData", data);
        }

        /// <summary>
        /// 异步调用服务端 SendData 方法；实现为同步远程调用，并非真正的异步
        /// </summary>
        /// <param name="data">参数 data</param>
        /// <returns>远程调用结果</returns>
        public Byte[] SendDataAsync(Byte[] data)
        {
            return _serviceConsumer.RemoteCall<Byte[]>("HelloService", "SendData", data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// UserInfo 实体
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// 属性 ID
        /// </summary>
        public Int32 ID
        {
            get;set;
        }

        /// <summary>
        /// 属性 UserName
        /// </summary>
        public String UserName
        {
            get;set;
        }

        /// <summary>
        /// 属性 Birthday
        /// </summary>
        public DateTime Birthday
        {
            get;set;
        }

        /// <summary>
        /// 属性 Score
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
    /// ActionResult<T> 实体
    /// </summary>
    public class ActionResult<T>
    {
        /// <summary>
        /// 属性 Success
        /// </summary>
        public Boolean Success
        {
            get;set;
        }

        /// <summary>
        /// 属性 Error
        /// </summary>
        public String Error
        {
            get;set;
        }

        /// <summary>
        /// 属性 Code
        /// </summary>
        public Int32 Code
        {
            get;set;
        }

        /// <summary>
        /// 属性 Data
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
    /// ReturnEnum 枚举
    /// </summary>
    public enum ReturnEnum:Int32
    {
        /// <summary>
        /// 枚举值 Big
        /// </summary>
        Big=0,
        /// <summary>
        /// 枚举值 Bigger
        /// </summary>
        Bigger=1,
        /// <summary>
        /// 枚举值 Biggest
        /// </summary>
        Biggest=2,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// EnumServiceType 枚举
    /// </summary>
    public enum EnumServiceType:Int32
    {
        /// <summary>
        /// 枚举值 Good
        /// </summary>
        Good=1,
        /// <summary>
        /// 枚举值 Better
        /// </summary>
        Better=2,
        /// <summary>
        /// 枚举值 Best
        /// </summary>
        Best=3,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    /// <summary>
    /// GroupInfo 实体
    /// </summary>
    public class GroupInfo
    {
        /// <summary>
        /// 属性 GroupID
        /// </summary>
        public Int32 GroupID
        {
            get;set;
        }

        /// <summary>
        /// 属性 Name
        /// </summary>
        public String Name
        {
            get;set;
        }

        /// <summary>
        /// 属性 IsTemporary
        /// </summary>
        public Boolean IsTemporary
        {
            get;set;
        }

        /// <summary>
        /// 属性 Created
        /// </summary>
        public DateTime Created
        {
            get;set;
        }

        /// <summary>
        /// 属性 Creator
        /// </summary>
        public UserInfo Creator
        {
            get;set;
        }

        /// <summary>
        /// 属性 Users
        /// </summary>
        public List<UserInfo> Users
        {
            get;set;
        }

        /// <summary>
        /// 属性 Limit
        /// </summary>
        public Dictionary<Int32,UserInfo> Limit
        {
            get;set;
        }

    }
}

