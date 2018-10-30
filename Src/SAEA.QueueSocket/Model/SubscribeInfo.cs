/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Model
*文件名： SubscribeInfo
*版本号： V3.2.1.1
*唯一标识：516e7423-55bc-4d53-83b2-5b5cd36827df
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/6 15:38:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/6 15:38:03
*修改人： yswenli
*版本号： V3.2.1.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.QueueSocket.Model
{
    [Serializable]
    class SubscribeInfo
    {
        public string Name
        {
            get; set;
        }

        public string Topic
        {
            get; set;
        }
    }
}
