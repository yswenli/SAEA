/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Net
*文件名： RSocketMsg
*版本号： V3.6.2.2
*唯一标识：39ab9c1a-8998-4b20-8078-4a8155b87ba8
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 15:15:52
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 15:15:52
*修改人： yswenli
*版本号： V3.6.2.2
*描述：
*
*****************************************************************************/
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Net
{
    /// <summary>
    /// RPC数据传输实体
    /// </summary>
    public class RSocketMsg : IDisposable
    {

        const int MIN = (1 + 4 + 8 + 4 + 0 + 4 + 0 + 0);


        public byte Type
        {
            get; set;
        }

        public uint Total
        {
            get; set;
        }

        public long SequenceNumber
        {
            get; set;
        }

        public uint SLen
        {
            get; set;
        }

        public string ServiceName
        {
            get; set;
        }

        public uint MLen
        {
            get; set;
        }

        public string MethodName
        {
            get; set;
        }

        public byte[] Data
        {
            get; set;
        }
        public RSocketMsg(RSocketMsgType type) : this(type, string.Empty)
        {

        }
        public RSocketMsg(RSocketMsgType type, string name) : this(type, name, string.Empty)
        {

        }
        public RSocketMsg(RSocketMsgType type, string name, string topic) : this(type, name, topic, null)
        {

        }
        public RSocketMsg(RSocketMsgType type, string name, string topic, byte[] data)
        {
            this.Type = (byte)type;
            this.ServiceName = name;
            this.MethodName = topic;
            this.Data = data;
        }

        public void Dispose()
        {
            this.ServiceName = this.MethodName =  string.Empty;
            if (this.Data != null) Array.Clear(this.Data, 0, this.Data.Length);
            this.Data = null;
            this.Total = this.SLen = this.MLen;
            this.Type = 0;
        }
    }
}
