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
*命名空间：SAEA.RedisSocket.Model
*文件名： ResponseExtention
*版本号： v26.4.23.1
*唯一标识：e9b703cc-6cf3-4ae3-97d3-e2666493ab9f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/07/24 09:57:22
*描述：ResponseExtention接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using SAEA.Common;
using System.Collections.Generic;
using SAEA.RedisSocket.Core;

namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// 把命令应答文本转换为强类型集合的扩展方法
    /// </summary>
    public static class ResponseExtention
    {

        /// <summary>
        /// 把应答文本拆分为字符串列表，末尾的分隔残余项会被忽略
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">命令应答</param>
        /// <returns>应答项列表，应答为空时返回 null</returns>
        public static List<string> ToList<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<string> list = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                list = new List<string>();

                var arr = source.Data.Split(RedisCoder.SEPARATOR);

                for (int i = 0; i < arr.Length; i++)
                {
                    if (i + 1 == arr.Length) break;
                    list.Add(arr[i]?.TrimEnd());
                }
            }

            return list;
        }

        /// <summary>
        /// 把应答文本按相邻两项组成键值对写入字典
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">命令应答</param>
        /// <returns>键值字典，应答为空时返回 null</returns>
        public static Dictionary<string, string> ToDic<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            Dictionary<string, string> dic = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                dic = new Dictionary<string, string>();

                var arr = source.Data.Split(RedisCoder.SEPARATOR);

                for (int i = 0; i < arr.Length; i++)
                {
                    if (i + 1 == arr.Length) break;
                    if (i % 2 == 1)
                    {
                        dic.Add(arr[i - 1], arr[i]);
                    }
                }
            }

            return dic;
        }


        /// <summary>
        /// 把应答文本按成员值与分值交替的规则转换为有序集合成员列表
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">带 WITHSCORES 的有序集合命令应答</param>
        /// <returns>有序集合成员列表，分值解析失败时该成员分值为 0，应答为空时返回 null</returns>
        public static List<ZItem> ToZList<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<ZItem> result = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                var arr = source.Data.Split(RedisCoder.SEPARATOR);

                if (arr != null && arr.Length > 0)
                {
                    result = new List<ZItem>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        ZItem zItem = new ZItem();

                        var val = arr[i];

                        if (!string.IsNullOrEmpty(val))
                        {
                            if (i + 1 < arr.Length)
                            {
                                double score = 0D;
                                double.TryParse(arr[i + 1], out score);
                                i++;
                                zItem.Value = val?.TrimEnd();
                                zItem.Score = score;
                                result.Add(zItem);
                            }
                        }
                    }
                }
            }


            return result;
        }

        /// <summary>
        /// 把应答文本按相邻两项组成键值对写入字典，重复的键会被跳过
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">命令应答</param>
        /// <returns>键值字典，缺值的末项对应空字符串，应答为空时返回 null</returns>
        public static Dictionary<string, string> ToKeyValues<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            Dictionary<string, string> keyValuePairs = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                var arr = source.Data.Split(RedisCoder.SEPARATOR);

                if (arr != null && arr.Length > 0)
                {
                    keyValuePairs = new Dictionary<string, string>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        var key = arr[i];

                        if (!string.IsNullOrEmpty(key) && !keyValuePairs.ContainsKey(key))
                        {
                            if (i + 1 < arr.Length)
                            {
                                keyValuePairs.Add(key, arr[i + 1]);
                                i++;
                            }
                            else
                            {
                                keyValuePairs.Add(key, string.Empty);
                            }
                        }
                    }
                }
            }

            return keyValuePairs;
        }

        /// <summary>
        /// 解析游标扫描应答，首项为游标、其余为数据项
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">SCAN、HSCAN、SSCAN、ZSCAN 的应答</param>
        /// <returns>含新游标与数据项的扫描结果，应答项格式不符时抛异常</returns>
        public static ScanResponse ToScanResponse<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            var scanResponse = new ScanResponse();

            var dataArr = source.Data.Split(RedisCoder.SEPARATOR);

            var doffset = 0;

            int.TryParse(dataArr[0].Split(":", StringSplitOptions.RemoveEmptyEntries)[1], out doffset);

            scanResponse.Offset = doffset;

            List<string> datas = new List<string>();

            for (int i = 1; i < dataArr.Length; i++)
            {
                if (string.IsNullOrEmpty(dataArr[i])) continue;

                datas.Add(dataArr[i]?.TrimEnd('\r', '\n'));
            }
            scanResponse.Data = datas;
            return scanResponse;
        }


        /// <summary>
        /// 把扫描结果中的字段与值两两组合为哈希表
        /// </summary>
        /// <param name="source">扫描结果</param>
        /// <returns>字段与值对应的哈希表，数据为空时 Data 为 null</returns>
        public static HScanResponse ToHScanResponse(this ScanResponse source)
        {
            if (source == null) return null;

            Dictionary<string, string> data = null;

            if (source.Data != null && source.Data.Count > 0)
            {
                data = new Dictionary<string, string>();

                for (int i = 0; i < source.Data.Count; i++)
                {
                    var key = source.Data[i];

                    if (!string.IsNullOrEmpty(key))
                    {
                        if (i + 1 < source.Data.Count)
                        {
                            data[key] = source.Data[i + 1];
                            i++;
                        }
                        else
                        {
                            data[key] = string.Empty;
                        }
                    }
                }
            }

            var result = new HScanResponse()
            {
                Offset = source.Offset,
                Data = data
            };

            return result;
        }

        /// <summary>
        /// 把扫描结果转换为有序集合成员，成员值与分值交替出现
        /// </summary>
        /// <param name="source">扫描结果</param>
        /// <returns>含游标与成员列表的扫描结果，数据为空时 Data 为 null</returns>
        public static ZScanResponse ToZScanResponse(this ScanResponse source)
        {
            if (source == null) return null;

            List<ZItem> data = null;

            if (source.Data != null && source.Data.Count > 0)
            {
                data = new List<ZItem>();

                for (int i = 0; i < source.Data.Count; i++)
                {
                    var zi = new ZItem();

                    zi.Value = source.Data[i];

                    if (!string.IsNullOrEmpty(zi.Value) && zi.Value.IndexOf("\r\n$") > -1)
                    {
                        var arr = zi.Value.Split("\r\n");
                        zi.Value = arr[0];
                        zi.Score = double.Parse(arr[2]);
                    }
                    else
                    {
                        var score = 0D;

                        if (i + 1 < source.Data.Count)
                        {
                            double.TryParse(source.Data[i + 1], out score);
                            i++;
                        }
                        zi.Score = score;
                    }                    

                    data.Add(zi);
                }
            }

            var result = new ZScanResponse()
            {
                Offset = source.Offset,
                Data = data
            };

            return result;
        }


        /// <summary>
        /// 把应答文本按经度、纬度成对解析为坐标列表
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">GEOPOS 等返回坐标的命令应答</param>
        /// <returns>坐标列表，应答项不足两项或为空时返回 null</returns>
        public static List<GeoNum> ToGeoNums<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<GeoNum> result = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                var arr = source.Data.Split(RedisCoder.SEPARATOR, StringSplitOptions.RemoveEmptyEntries);

                if (arr.Length > 1)
                {
                    result = new List<GeoNum>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (i % 2 == 1)
                        {
                            var geoNum = new GeoNum()
                            {
                                Lng = double.Parse(arr[i - 1]),
                                Lat = double.Parse(arr[i])
                            };
                            result.Add(geoNum);
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 把应答文本按名称、距离、经度、纬度四项一组解析为带距离的地理信息
        /// </summary>
        /// <typeparam name="T">应答实体类型</typeparam>
        /// <param name="source">带 WITHDIST、WITHCOORD 的 GEORADIUS 应答</param>
        /// <returns>带距离的地理信息列表，应答项不足或为空时返回 null</returns>
        public static List<GeoDistInfo> ToGeoDistInfos<T>(this ResponseData<T> source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<GeoDistInfo> result = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                var arr = source.Data.Split(RedisCoder.SEPARATOR, StringSplitOptions.RemoveEmptyEntries);

                if (arr.Length > 1)
                {
                    result = new List<GeoDistInfo>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        if (i % 4 == 3)
                        {
                            var geoDistInfo = new GeoDistInfo()
                            {
                                Name = arr[i - 3],
                                Dist = double.Parse(arr[i - 2]),
                                Lng = double.Parse(arr[i - 1]),
                                Lat = double.Parse(arr[i])
                            };
                            result.Add(geoDistInfo);
                        }
                    }
                }
            }

            return result;
        }
    }
}