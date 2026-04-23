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
*命名空间：SAEA.Http
*文件名： HttpSessionManager
*版本号： v26.4.23.1
*唯一标识：6db18cd0-7e38-4127-b674-fcbaebb6a3b2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/04/17 18:23:22
*描述：HttpSessionManager接口
*
*=====================================================================
*修改标记
*修改时间：2021/04/17 18:23:22
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpSessionManager接口
*
*****************************************************************************/
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