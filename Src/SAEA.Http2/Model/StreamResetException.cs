/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：StreamResetException
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 17:10:24
*描述：
*=====================================================================
*修改时间：2019/6/27 17:10:24
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    public class StreamResetException : Exception
    {
    }

    public class ConnectionClosedException : Exception
    {
    }

    public class ConnectionExhaustedException : Exception
    {
        public override string ToString()
        {
            return "由于已使用最大传出流ID，连接已用尽。要创建其他流，需要创建新连接";
        }
    }
}
