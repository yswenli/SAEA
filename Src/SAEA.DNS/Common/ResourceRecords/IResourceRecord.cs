/****************************************************************************
*项目名称：SAEA.DNS
*CLR 版本：3.0
*机器名称：WENLI-PC
*命名空间：SAEA.DNS.Common.ResourceRecords
*类 名 称：IResourceRecord
*版 本 号：v5.0.0.1
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/11/28 22:43:28
*描述：
*=====================================================================
*修改时间：2019/11/28 22:43:28
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Common.ResourceRecords
{
    public interface IResourceRecord : IMessageEntry {
        TimeSpan TimeToLive { get; }
        int DataLength { get; }
        byte[] Data { get; }
    }
}
