/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Model
*文件名： ChannelInfo
*版本号： v7.0.0.1
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 15:54:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 15:54:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/
using System;
using System.IO;
using System.Net.Sockets;

using SAEA.Common;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 表示通道信息的类
    /// </summary>
    public class ChannelInfo
    {
        /// <summary>
        /// 获取或设置通道的唯一标识符
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 获取或设置客户端的Socket对象
        /// </summary>
        public Socket ClientSocket { get; set; }

        /// <summary>
        /// 获取或设置通道的流对象
        /// </summary>
        public Stream Stream { get; set; }

        /// <summary>
        /// 获取或设置通道的过期时间
        /// </summary>
        public DateTime Expired { get; set; } = DateTimeHelper.Now;
    }
}
