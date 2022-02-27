/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisDataBase
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
using SAEA.RedisSocket.Core.Batches;
using SAEA.RedisSocket.Model;
using System;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis数据库操作类
    /// </summary>
    public partial class RedisDataBase 
    {
        /// <summary>
        /// 连接包装类
        /// </summary>
        internal RedisConnection RedisConnection { get; private set; }

        RedisLock _redisLock;

        /// <summary>
        /// redis数据库操作类
        /// </summary>
        /// <param name="cnn"></param>
        internal RedisDataBase(RedisConnection cnn)
        {
            RedisConnection = cnn;
            _redisLock = new RedisLock(RedisConnection);
        }

        #region bache

        /// <summary>
        /// 创建一个批量操作对象
        /// </summary>
        /// <returns></returns>
        public IBatch CreatedBatch()
        {
            return new Batch(this);
        }

        #endregion

        #region SCAN
        public ScanResponse Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScan(RequestType.SCAN, offset, pattern, count);
        }

        public HScanResponse HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.HSCAN, hid, offset, pattern, count).ToHScanResponse();
        }

        public ScanResponse SScan(string sid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.SSCAN, sid, offset, pattern, count);
        }

        public ZScanResponse ZScan(string zid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.ZSCAN, zid, offset, pattern, count).ToZScanResponse();
        }
        #endregion

        #region Pub/Sub
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithKeyValue(RequestType.PUBLISH, channel, value).Data, out result);
            return result;
        }

        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            RedisConnection.DoSub(channels, onMsg);
        }

        public void UNSUBSCRIBE(string channel)
        {
            RedisConnection.DoWithKey(RequestType.UNSUBSCRIBE, channel);
        }
        #endregion

        #region Lock
        public bool Lock(string key, int seconds)
        {
            return _redisLock.Lock(key, seconds);
        }
        public void Unlock(string key = "")
        {
            _redisLock.Unlock(key);
        }
        #endregion

    }
}
