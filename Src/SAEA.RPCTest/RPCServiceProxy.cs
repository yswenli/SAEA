/*******
*此代码为SAEA.RPCGenerater生成 2018-05-25 15:34:12
*******/

using System;
using SAEA.RPC.Consumer;
using SAEA.RPCTest.Consumer.Model;
using SAEA.RPCTest.Consumer.Service;

namespace SAEA.RPCTest.Consumer
{
    public class RPCServiceProxy
    {
        ServiceConsumer _serviceConsumer;
        public RPCServiceProxy(string uri = "rpc://127.0.0.1:39654") : this(new Uri(uri)) { }
        public RPCServiceProxy(Uri uri)
        {
            _serviceConsumer = new ServiceConsumer(uri);
            _helloService = new HelloService(_serviceConsumer);
        }
        HelloService _helloService;
        public HelloService HelloService
        {
            get { return _helloService; }
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    public class HelloService
    {
        ServiceConsumer _serviceConsumer;
        public HelloService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }
        public String Hello()
        {
            return _serviceConsumer.RemoteCall<String>("HelloService", "Hello");
        }
        public Int32 Plus(Int32 x, Int32 y)
        {
            return _serviceConsumer.RemoteCall<Int32>("HelloService", "Plus", x, y);
        }
        public UserInfo Update(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }
        public GroupInfo GetGroupInfo(Int32 id)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("HelloService", "GetGroupInfo", id);
        }
        public Byte[] SendData(Byte[] data)
        {
            return _serviceConsumer.RemoteCall<Byte[]>("HelloService", "SendData", data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public class UserInfo
    {
        public Int32 ID
        {
            get; set;
        }
        public String UserName
        {
            get; set;
        }
        public DateTime Birthday
        {
            get; set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public class GroupInfo
    {
        public Int32 GroupID
        {
            get; set;
        }
        public String Name
        {
            get; set;
        }
        public Boolean IsTemporary
        {
            get; set;
        }
        public DateTime Created
        {
            get; set;
        }
        public UserInfo Creator
        {
            get; set;
        }
    }
}

