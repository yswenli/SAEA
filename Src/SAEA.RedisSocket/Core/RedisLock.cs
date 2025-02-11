﻿/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisLock
*版本号： v7.0.0.1
*唯一标识：3d4f939c-3fb9-40e9-a0e0-c7ec773539ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/22 10:37:15
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/22 10:37:15
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RedisSocket.Model;
using System;

namespace SAEA.RedisSocket.Core
{
    class RedisLock
    {
        RedisConnection _cnn;

        object _syncLocker = new object();
        string _prefix = "redis_lock_";
        string _key = string.Empty;

        public RedisLock(RedisConnection cnn)
        {
            _cnn = cnn;
        }

        private DateTime GetDateTime()
        {
            DateTime dt = new DateTime();
            DateTime.TryParse(_cnn.DoWithKey(RequestType.GET, _key).Data, out dt);
            return dt;
        }

        private DateTime GetSetDateTime(string value)
        {
            DateTime dt = new DateTime();

            var result = GetSet(_key, value);

            if (string.IsNullOrEmpty(result))
            {
                result = value;

                if (DateTime.TryParse(result, out dt))
                {
                    var seconds = (dt - DateTimeHelper.Now).TotalSeconds;
                    _cnn.DoExpire(_key, (int)seconds);
                }
            }
            else
            {
                DateTime.TryParse(result, out dt);
            }
            return dt;
        }

        /// <summary>
        /// 设置指定 key 的值，并返回 key 旧的值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>返回给定 key 的旧值。 当 key 没有旧值时，即 key 不存在时，返回 nil 。当 key 存在但不是字符串类型时，返回一个错误。</returns>
        public string GetSet(string key, string value)
        {
            return _cnn.DoWithKeyValue(RequestType.GETSET, key, value).Data;
        }



        /// <summary>
        /// 命令在指定的 key 不存在时，为 key 设置指定的值。
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>若存在，返回false,否则返回true</returns>
        public bool SetNX(string key, string value)
        {
            if (_cnn.DoWithKeyValue(RequestType.SETNX, key, value).Data == "1")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 启用分布式锁
        /// </summary>
        /// <param name="key"></param>
        /// <param name="seconds"></param>
        /// <returns>false 表示锁过期，true表示锁成功，阻塞过程表示正在等待锁</returns>
        public bool Lock(string key, int seconds = 30)
        {
            lock (_syncLocker)
            {
                bool result = true;

                _key = string.Format("{0}{1}", _prefix, key);

                string expiredStr = DateTimeHelper.Now.AddSeconds(seconds).ToFString();

                while (!SetNX(_key, expiredStr))
                {
                    if (GetDateTime() < DateTimeHelper.Now && GetSetDateTime(expiredStr) < DateTimeHelper.Now)
                    {
                        result = false;
                        break;
                    }
                    else
                    {
                        ThreadHelper.SpinWait(1);
                    }
                }
                return result;
            }
        }
        /// <summary>
        /// 移除锁
        /// </summary>
        /// <param name="key"></param>
        public void Unlock(string key = "")
        {
            if (string.IsNullOrEmpty(key))
            {
                key = _key;
            }
            _cnn.DoWithKey(RequestType.DEL, key);
        }
    }
}
