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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAEA.RedisSocket.Core.Batches
{
    /// <summary>
    /// 批量操作方法
    /// </summary>
    public interface IBatch
    {
        /// <summary>
        /// 执行操作
        /// </summary>
        /// <returns></returns>
        List<object> Execute(); 

        void AppendAsync( string key, string value);

        void DecrementAsync( string key);
        void DecrementByAsync( string key, int num);
        void DelAsync( params string[] keys);
        void ExistsAsync( string key);
        void ExpireAsync(string key, int seconds);
        void ExpireAtAsync(string key, DateTime dateTime);
        void ExpireAtAsync(string key, int timestamp);
        void GeoAddAsync( string key, params GeoItem[] items);
        void GeoDistAsync( string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m);        
        void GetAsync( string key);
        void GetSetAsync( string key, string value);
        void HDelAsync( string hid, string key);
        void HDelAsync( string hid, string[] keys);
        void HExistsAsync( string hid, string key);
        void HGetAsync( string hid, string key);
        void HIncrementByAsync( string hid, string key, int num);
        void HIncrementByFloatAsync( string hid, string key, float num);
        void HLenAsync( string hid);
        void HMSetAsync( string hid, Dictionary<string, string> keyvalues);
        void HSetAsync( string hid, string key, string value);
        void HStrLenAsync( string hid, string key);
        void IncrementAsync( string key);
        void IncrementByAsync( string key, int num);
        void IncrementByFloatAsync( string key, float num);
        void LenAsync( string key);
        void LIndexAsync( string key, int index);
        void LInsertAsync( string key, string pivot, bool isBefore, string value);
        void LLenAsync( string key);
        void LPopAsync( string key);
        void LPushAsync( string key, List<string> lists);
        void LPushAsync( string key, string value);
        void LPushXAsync( string key, string value);
        void LRemoveAsync( string key, int count, string value);
        void LSetAsync( string key, int index, string value);
        void LTrimAsync( string key, int begin = 0, int end = -1);
        void MSetAsync( Dictionary<string, string> dic);
        void MSetNxAsync( Dictionary<string, string> dic);
        void PersistAsync( string key);
        void PttlAsync( string key);
        void RandomKeyAsync(TimeSpan timeSpan);
        void RenameAsync( string oldKey, string newKey);
        void RPopAsync( string key);
        void RpopLPushAsync( string source, string destination);
        void RPushAsync( string key, List<string> values);
        void RPushAsync( string key, string value);
        void RPushXAsync( string key, string value);
        void SAddAsync( string key, string value);
        void SAddAsync( string key, string[] value);
        void SDiffStoreAsync( string destination, params string[] keys);
        void SetAsync(string key, string value, int seconds);
        void SetAsync( string key, string value);
        void SExistsAsync( string key, string value);
        void SInterStoreAsync( string destination, params string[] keys);
        void SLenAsync( string key);
        void SPopAsync( string key);
        void SRandMemeberAsync( string key);
        void SRemoveAsync( string key, params string[] values);
        void SUnionStoreAsync( string destination, params string[] keys);
        void TtlAsync( string key);
        void ZAddAsync( string key, Dictionary<double, string> scoreVals);
        void ZAddAsync( string key, string value, double score);
        void ZCountAsync( string key, double begin = -2147483648, double end = 2147483647);
        void ZIncrByAsync( string key, double increment, string value);
        void ZIncrByAsync( string key, long increment, string value);
        void ZLenAsync( string key);
        void ZLexCountAsync( string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20);
        void ZRankAsync( string key, string value);
        void ZRemoveAsync( string key, string[] values);
        void ZRemoveByLexAsync( string key, double min = double.MinValue, double max = double.MaxValue, long offset = -1, int count = 20);
        void ZRemoveByRankAsync( string key, double start = 0, double stop = -1);
        void ZRemoveByScoreAsync( string key, double min = 0, double max = double.MaxValue, RangType rangType = RangType.None);
        void ZRevRankAsync( string key, string value);
        void ZScoreAsync( string key, string value);
    }
}