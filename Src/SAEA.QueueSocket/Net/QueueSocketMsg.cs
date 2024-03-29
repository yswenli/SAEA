﻿/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Net
*文件名： QueueSocketMsg
*版本号： v7.0.0.1
*唯一标识：69a2f1bc-89b4-4e9b-be5b-ae24301d0409
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/5 18:06:01
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/5 18:06:01
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.QueueSocket.Type;
using SAEA.Sockets.Interface;

using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.QueueSocket.Net
{
    /// <summary>
    /// 队列消息实体
    /// </summary>
    public class QueueSocketMsg : IDisposable
    {

        const int MIN = (1 + 4 + 4 + 0 + 4 + 0 + 0);

        public int Total
        {
            get; set;
        }

        public byte Type
        {
            get; set;
        }

        public int NLen
        {
            get; set;
        }

        public string Name
        {
            get; set;
        }

        public int TLen
        {
            get; set;
        }

        public string Topic
        {
            get; set;
        }

        public byte[] Data
        {
            get; set;
        }

        public QueueSocketMsg(QueueSocketMsgType type) : this(type, null)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name) : this(type, name, null)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic) : this(type, name, topic, null)
        {

        }
        public QueueSocketMsg(QueueSocketMsgType type, string name, string topic, byte[] data)
        {
            this.Type = (byte)type;
            this.Name = name;
            this.Topic = topic;
            this.Data = data;
        }

        public void Dispose()
        {
            if (this.Data != null && Data.Length > 0)
            {
                Array.Clear(Data, 0, Data.Length);
            }
            this.Total = this.NLen = this.TLen;
            this.Type = 0;
        }
    }
}
