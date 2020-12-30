/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Model
*文件名： ResponseData
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

using SAEA.RedisSocket.Interface;
using System;

namespace SAEA.RedisSocket.Model
{
    public class ResponseData : IResult
    {
        public ResponseType Type
        {
            get; set;
        }

        string _data = string.Empty;

        public string Data
        {
            get
            {
                return _data.TrimEnd('\r', '\n');
            }
            set
            {
                _data = value;
            }
        }

        public new string ToString()
        {
            return $"ResponseData\r\n\tType:{this.Type}\r\n\tData:{this.Data}";
        }
    }
}
