/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.QueueSocket.Model
*文件名： BindInfo
*版本号： V3.5.9.1
*唯一标识：39591e3c-ef47-46c8-b529-6f3d01b863d7
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/9 16:12:27
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/9 16:12:27
*修改人： yswenli
*版本号： V3.5.9.1
*描述：
*
*****************************************************************************/
using System;

namespace SAEA.QueueSocket.Model
{
    class BindInfo
    {
        public string Name
        {
            get; set;
        }
        public string SessionID
        {
            get; set;
        }
        public string Topic
        {
            get; set;
        }
        public bool Flag
        {
            get; set;
        }

        public DateTime Expired
        {
            get; set;
        }
    }
}
