/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.RedisSocket.Interface
*文件名： IRedisConnection
*版本号： V3.1.1.0
*唯一标识：3806bd74-f304-42b2-ab04-3e219828fa60
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*修改时间：2018/10/22 16:16:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/22 16:16:57
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/
using System;
using SAEA.RedisSocket.Core;
using SAEA.RedisSocket.Model;

namespace SAEA.RedisSocket.Interface
{
    public interface IRedisConnection
    {
        DateTime Actived { get; }
        bool IsConnected { get; }
        RedisCoder RedisCoder { get; }
        RedisServerType RedisServerType { get; set; }
        string IPPort { get; set; }

        event Action<string> OnDisconnected;
        void Connect();
        void Dispose();
        void KeepAlived(Action action);
        void Quit();
        void Send(string cmd);
    }
}