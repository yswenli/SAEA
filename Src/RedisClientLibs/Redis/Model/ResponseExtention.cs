/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Model
*文件名： ResponseExtention
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using SAEA.Common;

namespace SAEA.RedisSocket.Model
{
    public static class ResponseExtention
    {
        static string _enter = "\r\n";

        public static List<T> ToList<T>(this ResponseData source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<T> list = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                list = new List<T>();

                var arr = source.Data.Split(_enter);

                for (int i = 0; i < arr.Length; i++)
                {
                    if (i + 1 == arr.Length) break;

                    T val = (T)Convert.ChangeType(arr[i], typeof(T)); ;

                    list.Add(val);
                }
            }

            return list;
        }


        public static List<ZItem> ToList(this ResponseData source)
        {
            if (source == null) return null;

            if (source.Type == ResponseType.Error)
            {
                throw new Exception(source.Data);
            }

            List<ZItem> result = null;

            if (!string.IsNullOrEmpty(source.Data))
            {
                var arr = source.Data.Split(_enter);

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
                                zItem.Value = val;
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
                var arr = source.Data.Split(_enter);

                if (arr != null && arr.Length > 0)
                {
                    keyValuePairs = new Dictionary<string, string>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        var key = arr[i];

                        if (!string.IsNullOrEmpty(key))
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
            var scanResponse = new ScanResponse();

            var dataArr = source.Data.Split(_enter, StringSplitOptions.None);

            var doffset = 0;

            int.TryParse(dataArr[0].Split(":", StringSplitOptions.RemoveEmptyEntries)[1], out doffset);

            scanResponse.Offset = doffset;

            List<string> datas = new List<string>();

            for (int i = 1; i < dataArr.Length; i++)
            {
                if (i + 1 == dataArr.Length) break;
                datas.Add(dataArr[i]);
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
                        if (i + 1 <= source.Data.Count)
                        {
                            data.Add(key, source.Data[i + 1]);
                            i++;
                        }
                        else
                        {
                            data.Add(key, string.Empty);
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
    }
}
