/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisDataBase
*版本号： v5.0.0.1
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
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// redis数据库操作类
    /// </summary>
    public partial class RedisDataBase
    {
        RedisConnection _cnn;

        RedisLock _redisLock;

        internal RedisDataBase(RedisConnection cnn)
        {
            _cnn = cnn;
            _redisLock = new RedisLock(_cnn);
        }
        #region SCAN
        public ScanResponse Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScan(RequestType.SCAN, offset, pattern, count);
        }

        public HScanResponse HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.HSCAN, hid, offset, pattern, count).ToHScanResponse();
        }

        public ScanResponse SScan(string sid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.SSCAN, sid, offset, pattern, count);
        }

        public ZScanResponse ZScan(string zid, int offset = 0, string pattern = "*", int count = -1)
        {
            return _cnn.DoScanKey(RequestType.ZSCAN, zid, offset, pattern, count).ToZScanResponse();
        }
        #endregion

        #region Pub/Sub
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(_cnn.DoWithKeyValue(RequestType.PUBLISH, channel, value).Data, out result);
            return result;
        }

        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            _cnn.DoSub(channels, onMsg);
        }

        public void UNSUBSCRIBE(string channel)
        {
            _cnn.DoWithKey(RequestType.UNSUBSCRIBE, channel);
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
