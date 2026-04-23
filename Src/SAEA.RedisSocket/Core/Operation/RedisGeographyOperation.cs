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
*文件名： RedisGeographyOperation
*版本号： v26.4.23.1
*唯一标识：81117ef4-7f99-4a7f-9227-bb6b1035736c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/07/24 09:50:43
*描述：RedisGeographyOperation接口
*
*=====================================================================
*修改标记
*修改时间：2020/07/24 09:50:43
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisGeographyOperation接口
*
*****************************************************************************/
using SAEA.RedisSocket.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.RedisSocket.Core
{
    /// <summary>
    /// Geography
    /// </summary>
    public partial class RedisDataBase
    {
        /// <summary>
        /// 将给定的空间元素（纬度、经度、名字）添加到指定的键里面
        /// </summary>
        /// <param name="key"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public int GeoAdd(string key, params GeoItem[] items)
        {
            var result = -1;

            if (items == null || !items.Any())
            {
                throw new Exception("params must not allow null");
            }

            var list = new List<string>();
            list.Add(key);
            list.AddRange(GeoItem.ToParams(items));

            int.TryParse(RedisConnection.DoWithMutiParams(RequestType.GEOADD, list.ToArray()).Data, out result);

            return result;
        }


        /// <summary>
        /// 从键里面返回所有给定位置元素的位置（经度和纬度）
        /// </summary>
        /// <param name="key"></param>
        /// <param name="memebers"></param>
        /// <returns></returns>
        public List<GeoNum> GeoPos(string key, params string[] memebers)
        {
            return RedisConnection.DoBatchWithIDKeys(RequestType.GEOPOS, key, memebers).ToGeoNums();
        }

        /// <summary>
        /// 返回两个给定位置之间的距离
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member1"></param>
        /// <param name="member2"></param>
        /// <param name="geoUnit"></param>
        /// <returns></returns>
        public double GeoDist(string key, string member1, string member2, GeoUnit geoUnit = GeoUnit.m)
        {
            double result = 0D;

            double.TryParse(RedisConnection.DoWithMutiParams(RequestType.GEODIST, key, member1, member2, geoUnit.ToString()).Data, out result);

            return result;
        }

        /// <summary>
        /// 以给定的经纬度为中心， 返回键包含的位置元素当中， 与中心的距离不超过给定最大距离的所有位置元素
        /// </summary>
        /// <param name="key"></param>
        /// <param name="lng"></param>
        /// <param name="lat"></param>
        /// <param name="dist"></param>
        /// <param name="geoUnit"></param>
        /// <param name="asc"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<GeoDistInfo> GeoRandius(string key, double lng, double lat, double dist, GeoUnit geoUnit = GeoUnit.m, bool asc = true, int count = 20)
        {
            return RedisConnection.DoWithMutiParams(RequestType.GEORADIUS, key, lng.ToString(), lat.ToString(), dist.ToString(), geoUnit.ToString(), "WITHDIST", "WITHCOORD", (asc ? "ASC" : "DESC"), "COUNT", count.ToString()).ToGeoDistInfos();
        }

        /// <summary>
        /// 找出位于指定范围内的元素， 但是 GEORADIUSBYMEMBER 的中心点是由给定的位置元素决定的， 而不是像 GEORADIUS 那样， 使用输入的经度和纬度来决定中心点
        /// </summary>
        /// <param name="key"></param>
        /// <param name="member"></param>
        /// <param name="dist"></param>
        /// <param name="geoUnit"></param>
        /// <param name="asc"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<GeoDistInfo> GeoRandiusByMember(string key, string member, double dist, GeoUnit geoUnit = GeoUnit.m, bool asc = true, int count = 20)
        {
            return RedisConnection.DoWithMutiParams(RequestType.GEORADIUSBYMEMBER, key, member, dist.ToString(), geoUnit.ToString(), "WITHDIST", "WITHCOORD", (asc ? "ASC" : "DESC"), "COUNT", count.ToString()).ToGeoDistInfos();
        }


    }
}
