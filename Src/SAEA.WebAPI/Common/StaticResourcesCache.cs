/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Http.Base
*文件名： StaticResourcesCache
*版本号： V1.0.0.0
*唯一标识：4eebbaa7-1781-4521-ab57-4bc9c8d43a84
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/8/5 13:31:23
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/8/5 13:31:23
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.BaseLibs.WebApi.Http.Base
{
    /// <summary>
    /// 静态资源缓存
    /// </summary>
    public static class StaticResourcesCache
    {
        static ConcurrentDictionary<string, byte[]> _cache = new ConcurrentDictionary<string, byte[]>();

        /// <summary>
        /// 增加或获取资源
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static byte[] GetOrAdd(string key, Func<string, byte[]> valueFactory)
        {
            return _cache.GetOrAdd(key, valueFactory);
        }

    }
}
