/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http.Http.Base
*文件名： StaticResourcesCache
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System.Collections.Concurrent;
using System.IO;

namespace SAEA.Http.Common
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
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static byte[] GetOrAdd(string key, string fileName)
        {
            return _cache.GetOrAdd(key, FileHelper.Read(fileName));
        }

        /// <summary>
        /// 判断文件是否存在
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool Exists(string fileName)
        {
            return File.Exists(fileName);
        }

        /// <summary>
        /// 是否是大文件
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsBigFile(string fileName)
        {
            var fileInfo = new FileInfo(fileName);

            //超过4M认为是大文件
            if (fileInfo.Length> 4194304)
            {
                return true;
            }

            return false;
        }

    }
}
