using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPCTest.Provider.Model
{
    /// <summary>
    /// 服务评价等级枚举，用作 RPC 枚举参数的序列化示例
    /// </summary>
    public enum EnumServiceType
    {
        /// <summary>
        /// 初级评价，值为 1
        /// </summary>
        Good = 1,
        /// <summary>
        /// 中级评价，值为 2
        /// </summary>
        Better = 2,
        /// <summary>
        /// 高级评价，值为 3
        /// </summary>
        Best = 3
    }

    /// <summary>
    /// 服务返回档位枚举，用作 RPC 枚举返回值的序列化示例
    /// </summary>
    public enum ReturnEnum
    {
        /// <summary>
        /// 小档位，值为 0
        /// </summary>
        Big,
        /// <summary>
        /// 中档位，值为 1
        /// </summary>
        Bigger,
        /// <summary>
        /// 大档位，值为 2
        /// </summary>
        Biggest
    }
}
