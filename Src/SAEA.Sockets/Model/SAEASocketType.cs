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
*命名空间：SAEA.Sockets.Model
*文件名： SAEASocketType
*版本号： v26.4.23.1
*唯一标识：d7c696dd-5811-4493-a4ab-5e659e8d55b0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：SAEASocketType类型枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Sockets.Model
{
    /// <summary>
    /// 套接字类型
    /// </summary>
    public enum SAEASocketType
    {
        /// <summary>
        /// TCP 套接字
        /// </summary>
        Tcp = 0,
        /// <summary>
        /// UDP 套接字
        /// </summary>
        Udp = 1
    }
}