/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.QueueSocket.Model
*文件名： PublishInfo
*版本号： v5.0.0.1
*唯一标识：ffe43b18-b60e-4660-81b9-354b856b8605
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/6 10:11:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/6 10:11:03
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.QueueSocket.Model
{
    [Serializable]
    class PublishInfo : SubscribeInfo
    {
        public byte[] Data
        {
            get; set;
        }
    }
}
