using SAEA.RPC;
using SAEA.RPCTest.Consumer.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Providers
{
    [RPCService]
    public class DicService
    {
        public Dictionary<int, UserInfo> Test(int id, Dictionary<int, UserInfo> data)
        {
            return data;
        }
    }
}
