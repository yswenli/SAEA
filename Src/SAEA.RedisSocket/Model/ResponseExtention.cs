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

namespace SAEA.RedisSocket.Model
{
    public static class ResponseExtention
    {
        static string _enter = "\r\n";

        public static Dictionary<string, string> ToKeyValues(this ResponseData result)
        {
            if (result == null) return null;

            if (result.Type == ResponseType.Error)
            {
                throw new Exception(result.Data);
            }

            Dictionary<string, string> keyValuePairs = null;

            if (!string.IsNullOrEmpty(result.Data))
            {
                var arr = result.Data.Split(_enter);

                if (arr != null && arr.Length > 0)
                {
                    keyValuePairs = new Dictionary<string, string>();

                    for (int i = 0; i < arr.Length; i++)
                    {
                        var key = arr[i];

                        if (!string.IsNullOrEmpty(key))
                        {
                            if (i + 2 <= arr.Length)
                            {
                                keyValuePairs.Add(key, arr[i + 2]);
                            }
                            else
                            {
                                keyValuePairs.Add(key, string.Empty);
                            }
                        }

                        i += 3;
                    }
                }
            }

            return keyValuePairs;
        }

        public static ScanResponse ToScanResponse(this ResponseData result)
        {
            var scanResponse = new ScanResponse();

            var dataArr = result.Data.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var doffset = 0;

            int.TryParse(dataArr[0].Split(":", StringSplitOptions.RemoveEmptyEntries)[1], out doffset);

            scanResponse.Offset = doffset;

            List<string> datas = new List<string>();

            for (int i = 1; i < dataArr.Length; i++)
            {
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
                        }
                        else
                        {
                            data.Add(key, string.Empty);
                        }
                    }

                    i += 1;
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

            List<ZScanItem> data = null;

            if (source.Data != null && source.Data.Count > 0)
            {
                data = new List<ZScanItem>();

                for (int i = 0; i < source.Data.Count; i++)
                {
                    var zi = new ZScanItem();

                    zi.Value = source.Data[i];

                    var score = 0D;

                    if (i + 1 <= source.Data.Count)
                    {
                        double.TryParse(source.Data[i + 1], out score);
                    }
                    zi.Score = score;

                    data.Add(zi);

                    i += 1;
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
