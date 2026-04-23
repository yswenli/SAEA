/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Net
*文件名： RSocketMsg
*版本号： v26.4.23.1
*唯一标识：5999d7de-2387-4e5e-bd79-85a959ceae59
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：RSocketMsg类
*
*=====================================================================
*修改标记
*修改时间：2018/05/25 17:28:26
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RSocketMsg类
*
*****************************************************************************/
using SAEA.RPC.Model;
using System;

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

        /// <summary>
        /// 数据是否来自内存池
        /// </summary>
        public bool IsPooled { get; set; }

        public RSocketMsg(RSocketMsgType type) : this(type, string.Empty)
        {

        }
        public RSocketMsg(RSocketMsgType type, string serviceName) : this(type, serviceName, string.Empty)
        {

        }
        public RSocketMsg(RSocketMsgType type, string serviceName, string method) : this(type, serviceName, method, null)
        {

        }
        public RSocketMsg(RSocketMsgType type, string serviceName, string method, byte[] data)
        {
            this.Type = (byte)type;
            this.ServiceName = serviceName;
            this.MethodName = method;
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