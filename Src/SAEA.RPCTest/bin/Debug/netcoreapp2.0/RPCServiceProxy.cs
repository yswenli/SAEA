/*******
*此代码为SAEA.RPCGenerater生成 2018-05-29 20:28:05
*******/

using System;
using System.Collections.Generic;
using SAEA.RPC.Consumer;
using SAEA.RPCTest.Consumer.Model;
using SAEA.RPCTest.Consumer.Service;

namespace SAEA.RPCTest.Consumer
{
    public class RPCServiceProxy
    {
        ServiceConsumer _serviceConsumer;
        public RPCServiceProxy(string uri = "rpc://127.0.0.1:39654") : this(new Uri(uri)){}
        public RPCServiceProxy(Uri uri)
        {
            _serviceConsumer = new ServiceConsumer(uri);
            _groupService = new GroupService(_serviceConsumer);
            _helloService = new HelloService(_serviceConsumer);
        }
        GroupService _groupService;
        public GroupService GroupService
        {
             get{ return _groupService; }
        }
        HelloService _helloService;
        public HelloService HelloService
        {
             get{ return _helloService; }
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    public class GroupService
    {
        ServiceConsumer _serviceConsumer;
        public GroupService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }
        public List<UserInfo> Update(List<UserInfo> users)
        {
            return _serviceConsumer.RemoteCall<List<UserInfo>>("GroupService", "Update", users);
        }
        public GroupInfo Add(String groupName, UserInfo user)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "Add", groupName, user);
        }
        public GroupInfo GetGroupInfo(Int32 id)
        {
            return _serviceConsumer.RemoteCall<GroupInfo>("GroupService", "GetGroupInfo", id);
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
            get;set;
        }
        public String UserName
        {
            get;set;
        }
        public DateTime Birthday
        {
            get;set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public class GroupInfo
    {
        public Int32 GroupID
        {
            get;set;
        }
        public String Name
        {
            get;set;
        }
        public Boolean IsTemporary
        {
            get;set;
        }
        public DateTime Created
        {
            get;set;
        }
        public UserInfo Creator
        {
            get;set;
        }
        public List<UserInfo> Users
        {
            get;set;
        }
    }
}

