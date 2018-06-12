
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAEA.RPC.Common;
using SAEA.RPCTest.Consumer.Model;

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
