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
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisDataBase
*版本号： v26.4.23.1
*唯一标识：58139320-3ac6-40bd-b748-c0bbe003ce8a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/20 10:04:58
*描述：RedisDataBase接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
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
        /// <summary>
        /// 游标遍历数据库中的键
        /// </summary>
        /// <param name="offset">游标位置，小于 0 时按 0 处理</param>
        /// <param name="pattern">键名匹配模式，为空时不发送 MATCH</param>
        /// <param name="count">每次返回的提示数量，小于 0 时不发送 COUNT</param>
        /// <returns>本次遍历结果，响应不是多行数据时返回 null</returns>
        public ScanResponse Scan(int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScan(RequestType.SCAN, offset, pattern, count);
        }

        /// <summary>
        /// 游标遍历哈希表的字段
        /// </summary>
        /// <param name="hid">哈希表键名</param>
        /// <param name="offset">游标位置，小于 0 时按 0 处理</param>
        /// <param name="pattern">字段名匹配模式，为空时不发送 MATCH</param>
        /// <param name="count">每次返回的提示数量，小于 0 时不发送 COUNT</param>
        /// <returns>键值交替的遍历结果</returns>
        public HScanResponse HScan(string hid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.HSCAN, hid, offset, pattern, count).ToHScanResponse();
        }

        /// <summary>
        /// 游标遍历集合中的成员
        /// </summary>
        /// <param name="sid">集合键名</param>
        /// <param name="offset">游标位置，小于 0 时按 0 处理</param>
        /// <param name="pattern">成员匹配模式，为空时不发送 MATCH</param>
        /// <param name="count">每次返回的提示数量，小于 0 时不发送 COUNT</param>
        /// <returns>本次遍历结果，响应不是多行数据时返回 null</returns>
        public ScanResponse SScan(string sid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.SSCAN, sid, offset, pattern, count);
        }

        /// <summary>
        /// 游标遍历有序集中的成员及其分值
        /// </summary>
        /// <param name="zid">有序集键名</param>
        /// <param name="offset">游标位置，小于 0 时按 0 处理</param>
        /// <param name="pattern">成员匹配模式，为空时不发送 MATCH</param>
        /// <param name="count">每次返回的提示数量，小于 0 时不发送 COUNT</param>
        /// <returns>含成员分值的遍历结果</returns>
        public ZScanResponse ZScan(string zid, int offset = 0, string pattern = "*", int count = -1)
        {
            return RedisConnection.DoScanKey(RequestType.ZSCAN, zid, offset, pattern, count).ToZScanResponse();
        }
        #endregion

        #region Pub/Sub
        /// <summary>
        /// 向指定频道发布消息
        /// </summary>
        /// <param name="channel">频道名</param>
        /// <param name="value">消息内容</param>
        /// <returns>接收到该消息的订阅者数量，解析失败时为 0</returns>
        public int Publish(string channel, string value)
        {
            var result = 0;
            int.TryParse(RedisConnection.DoWithKeyValue(RequestType.PUBLISH, channel, value).Data, out result);
            return result;
        }

        /// <summary>
        /// 订阅一个或多个频道，调用线程会在此阻塞并持续派发消息，直到退订
        /// </summary>
        /// <param name="onMsg">消息回调，参数依次为频道名与消息内容</param>
        /// <param name="channels">要订阅的频道集合</param>
        public void Suscribe(Action<string, string> onMsg, params string[] channels)
        {
            RedisConnection.DoSub(channels, onMsg);
        }

        /// <summary>
        /// 退订指定频道
        /// </summary>
        /// <param name="channel">频道名</param>
        public void UNSUBSCRIBE(string channel)
        {
            RedisConnection.DoWithKey(RequestType.UNSUBSCRIBE, channel);
        }
        #endregion

        #region Lock
        /// <summary>
        /// 以自旋方式获取分布式锁，同一实例内串行执行
        /// </summary>
        /// <param name="key">锁的业务键名，内部会拼上固定前缀</param>
        /// <param name="seconds">锁的过期秒数</param>
        /// <returns>获取成功返回 true，检测到持有锁已过期但抢占失败返回 false</returns>
        public bool Lock(string key, int seconds)
        {
            return _redisLock.Lock(key, seconds);
        }

        /// <summary>
        /// 释放分布式锁
        /// </summary>
        /// <param name="key">锁键名，为空时删除最近一次 Lock 使用的内部键</param>
        public void Unlock(string key = "")
        {
            _redisLock.Unlock(key);
        }
        #endregion

    }
}