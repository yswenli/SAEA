/*******
* 此代码为SAEA.RPC.Generater生成
* 尽量不要修改此代码 2019-03-28 15:13:49
*******/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.RPC.Consumer;
using SAEA.RPC.Model;
using SAEA.RPCTest.Consumer.Model;
using SAEA.RPCTest.Consumer.Service;

namespace SAEA.RPCTest.Consumer
{
    public class RPCServiceProxy
    {
        public event ExceptionCollector.OnErrHander OnErr;

        public event OnNoticedHandler OnNoticed;

        ServiceConsumer _serviceConsumer;

        public RPCServiceProxy(string uri = "rpc://127.0.0.1:39654") : this(uri, 4, 5, 10 * 1000) { }
        public RPCServiceProxy(string uri, int links = 4, int retry = 5, int timeOut = 10 * 1000)
        {
            ExceptionCollector.OnErr += ExceptionCollector_OnErr;

            _serviceConsumer = new ServiceConsumer(new Uri(uri), links, retry, timeOut);
            _serviceConsumer.OnNoticed += _serviceConsumer_OnNoticed;

            _En = new EnumService(_serviceConsumer);
            _Gr = new GroupService(_serviceConsumer);
            _He = new HelloService(_serviceConsumer);
            _Di = new DicService(_serviceConsumer);
            _Ge = new GenericService(_serviceConsumer);
        }
        private void ExceptionCollector_OnErr(string name, Exception ex)
        {
            OnErr?.Invoke(name, ex);
        }
        private void _serviceConsumer_OnNoticed(byte[] serializeData)
        {
            OnNoticed?.Invoke(serializeData);
        }
        public bool IsConnected
        {
            get { return _serviceConsumer.IsConnected; }
        }
        public void Dispose()
        {
            _serviceConsumer.Dispose();
        }
        EnumService _En;
        public EnumService EnumService
        {
            get { return _En; }
        }
        GroupService _Gr;
        public GroupService GroupService
        {
            get { return _Gr; }
        }
        HelloService _He;
        public HelloService HelloService
        {
            get { return _He; }
        }
        DicService _Di;
        public DicService DicService
        {
            get { return _Di; }
        }
        GenericService _Ge;
        public GenericService GenericService
        {
            get { return _Ge; }
        }

        public void RegistReceiveNotice()
        {
            _serviceConsumer.RegistReceiveNotice();
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    public class EnumService
    {
        ServiceConsumer _serviceConsumer;
        public EnumService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }
        public ReturnEnum GetEnum(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
        }
        public ReturnEnum GetEnumAsync(EnumServiceType est)
        {
            return _serviceConsumer.RemoteCall<ReturnEnum>("EnumService", "GetEnum", est);
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

        public UserInfo UpdateAsync(UserInfo info)
        {
            return _serviceConsumer.RemoteCall<UserInfo>("HelloService", "Update", info);
        }

        public Byte[] SendData(Byte[] data)
        {
            return _serviceConsumer.RemoteCall<Byte[]>("HelloService", "SendData", data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    public class DicService
    {
        ServiceConsumer _serviceConsumer;
        public DicService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }
        public Dictionary<Int32, UserInfo> Test(Int32 id, Dictionary<Int32, UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<Dictionary<Int32, UserInfo>>("DicService", "Test", id, data);
        }
    }
}

namespace SAEA.RPCTest.Consumer.Service
{
    public class GenericService
    {
        ServiceConsumer _serviceConsumer;
        public GenericService(ServiceConsumer serviceConsumer)
        {
            _serviceConsumer = serviceConsumer;
        }
        public ActionResult<UserInfo> Get(ActionResult<UserInfo> data)
        {
            return _serviceConsumer.RemoteCall<ActionResult<UserInfo>>("GenericService", "Get", data);
        }
        public List<String> GetListString()
        {
            return _serviceConsumer.RemoteCall<List<String>>("GenericService", "GetListString");
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public enum ReturnEnum : Int32
    {
        Big = 0,
        Bigger = 1,
        Biggest = 2,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public enum EnumServiceType : Int32
    {
        Good = 1,
        Better = 2,
        Best = 3,
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    [Serializable]
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

        public decimal Score
        {
            get; set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    [Serializable]
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
        public List<UserInfo> Users
        {
            get; set;
        }
        public Dictionary<Int32, UserInfo> Limit
        {
            get; set;
        }
    }
}

namespace SAEA.RPCTest.Consumer.Model
{
    public class ActionResult<T>
    {
        public Boolean Success
        {
            get; set;
        }
        public String Error
        {
            get; set;
        }
        public Int32 Code
        {
            get; set;
        }
        public T Data
        {
            get; set;
        }
    }
}

