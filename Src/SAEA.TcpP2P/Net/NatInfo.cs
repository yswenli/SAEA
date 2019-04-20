using System;
/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.TcpP2P.Net
*文件名： Receiver
*版本号： v4.3.3.7
*唯一标识：02774aed-635d-4731-82ec-daaace9ce96f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/12/3 21:46:22
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/12/3 21:46:22
*修改人： yswenli
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using System.Runtime.Serialization;

namespace SAEA.TcpP2P.Net
{
    [DataContract, Serializable]
    public class NatInfo
    {
        [DataMember]
        public string IP { get; set; }

        [DataMember]
        public int Port { get; set; }

        [DataMember]
        public bool IsMe { get; set; }

        public new string ToString()
        {
            return IP + ":" + Port;
        }
    }
}
