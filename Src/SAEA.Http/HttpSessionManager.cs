/****************************************************************************
*Copyright (c) 2018-2021yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Http
*文件名： HttpSessionManager
*版本号： v7.0.0.1
*唯一标识：2e43075f-a43d-4b60-bee1-1f9107e2d133
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/12/12 20:46:40
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/12/12 20:46:40
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
******************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Threading;

using SAEA.Common;
using SAEA.Common.Threading;

namespace SAEA.Http
{
    /// <summary>
    /// SessionManager
    /// </summary>
    internal static class HttpSessionManager
    {
        static ConcurrentDictionary<string, HttpSession> _cache = new ConcurrentDictionary<string, HttpSession>();

        static Random _random = new Random();

        /// <summary>
        /// SessionManager
        /// </summary>
        static HttpSessionManager()
        {
            TaskHelper.LongRunning(() =>
            {
                while (true)
                {
                    foreach (var item in _cache)
                    {
                        if (item.Value.Expired < DateTimeHelper.Now)
                        {
                            item.Value.Clear();
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                }
            });
        }

        /// <summary>
        /// 生成sessionID
        /// </summary>
        /// <returns></returns>
        public static string GeneratID()
        {
            var bytes = new byte[15];

            _random.NextBytes(bytes);

            return StringHelper.Substring(string.Join("", bytes), 0, 15);
        }

        /// <summary>
        /// 获取HttpSession
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static HttpSession GetIfNotExistsSet(string id)
        {
            var session = _cache.GetOrAdd(id, (k) =>
            {
                return new HttpSession(k);
            });
            session.Refresh();
            return session;
        }

        /// <summary>
        /// 移除httpSession
        /// </summary>
        /// <param name="id"></param>
        public static void Remove(string id)
        {
            if (_cache.TryRemove(id, out HttpSession httpSession))
            {
                httpSession.Dispose();
            }
        }

    }
}
