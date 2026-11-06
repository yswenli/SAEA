using SAEA.RPC;
using SAEA.RPCTest.Consumer.Model;
using System.Collections.Generic;

namespace SAEA.RPCTest.Providers
{
    /// <summary>
    /// 字典服务示例，验证字典类型参数与返回值的远程调用
    /// </summary>
    [RPCService]
    public class DicService
    {
        /// <summary>
        /// 回传传入的字典数据，用于验证键值对的序列化
        /// </summary>
        /// <param name="id">用户 ID，当前实现未使用</param>
        /// <param name="data">待回传的用户字典数据</param>
        /// <returns>与入参相同的用户字典数据</returns>
        public Dictionary<int, UserInfo> Test(int id, Dictionary<int, UserInfo> data)
        {
            return data;
        }
    }
}
