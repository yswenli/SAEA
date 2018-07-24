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
        public static ScanResponse ToScanResponse(this ResponseData result)
        {
            var scanResponse = new ScanResponse();

            var dataArr = result.Data.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            var doffset = 0;

            int.TryParse(dataArr[0].Split(new string[] { ":" }, StringSplitOptions.RemoveEmptyEntries)[1], out doffset);

            scanResponse.Offset = doffset;

            List<string> datas = new List<string>();

            for (int i = 1; i < dataArr.Length; i++)
            {
                datas.Add(dataArr[i]);
            }
            scanResponse.Data = datas;
            return scanResponse;
        }
    }
}
