/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RedisSocket.Core
*文件名： RedisConnection
*版本号： v4.1.2.5
*唯一标识：a22caf84-4c61-456e-98cc-cbb6cb2c6d6e
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/11/5 20:45:02
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/11/5 20:45:02
*修改人： yswenli
*版本号： v4.1.2.5
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.RedisSocket.Model
{
    public class RedisParamsNullException : Exception
    {
        public RedisParamsNullException() : this("redis 输入参数不能为null!")
        {

        }

        public RedisParamsNullException(string message) : base(message)
        {

        }
    }
}
