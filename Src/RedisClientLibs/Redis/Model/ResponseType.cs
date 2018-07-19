/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket
*文件名： ResponseType
*版本号： V1.0.0.0
*唯一标识：8f0de50d-fc9a-4b83-be84-6b497667b700
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 9:49:07
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 9:49:07
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Model
{
    public enum ResponseType : byte
    {
        OK = 1,
        Error = 2,
        Value = 3,
        Empty = 4,
        String = 5,
        Lines = 6,
        KeyValues = 7,
        Sub = 8,
        UnSub = 9,
        Undefined = 10,
        Redirect = 11,
    }
}
