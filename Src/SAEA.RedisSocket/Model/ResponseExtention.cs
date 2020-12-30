/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Model
*文件名： ResponseExtention
*版本号： v6.0.0.1
*唯一标识：bc48708f-e1e1-4b9e-be22-0cba54211c76
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 9:52:12
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 9:52:12
*修改人： yswenli
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/
using System;
using SAEA.Common;
using System.Collections.Generic;
using SAEA.RedisSocket.Core;

namespace SAEA.RedisSocket.Model
{
    public static class ResponseExtention
    {

        public static List<string> ToList(this ResponseData source)
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
                    list.Add(arr[i]);
                }
            }

            return list;
        }

        public static Dictionary<string, string> ToDic(this ResponseData source)
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


        public static List<ZItem> ToZList(this ResponseData source)
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

        public static Dictionary<string, string> ToKeyValues(this ResponseData source)
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

        public static ScanResponse ToScanResponse(this ResponseData source)
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

                    var score = 0D;

                    if (i + 1 < source.Data.Count)
                    {
                        double.TryParse(source.Data[i + 1], out score);
                        i++;
                    }
                    zi.Score = score;

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


        public static List<GeoNum> ToGeoNums(this ResponseData source)
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

        public static List<GeoDistInfo> ToGeoDistInfos(this ResponseData source)
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
