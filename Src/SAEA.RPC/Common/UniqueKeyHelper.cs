/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Common
*文件名： UniqueKeyHelper
*版本号： v26.4.23.1
*唯一标识：0a1efcbf-5b63-4a1f-bed8-4f428e5f56b0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：UniqueKeyHelper接口
*
*=====================================================================
*修改标记
*修改时间：2018/05/25 17:28:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：UniqueKeyHelper接口
*
*****************************************************************************/
using System.Threading;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// 唯一值工具类
    /// </summary>
    public static class UniqueKeyHelper
    {
        static long sNo = long.MinValue;

        static long maxVal = long.MaxValue - 1000000;
        /// <summary>
        /// 获取下一个值
        /// </summary>
        /// <returns></returns>
        public static long Next()
        {
            if (Interlocked.Increment(ref sNo) > maxVal)
            {
                sNo = long.MinValue;
            }
            return sNo;
        }
    }
}