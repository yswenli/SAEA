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
*命名空间：SAEA.Sockets.Interface
*文件名： ISyncBase
*版本号： v26.4.23.1
*唯一标识：c77ba7c6-36aa-487c-869a-be9ee74ec5c6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：ISyncBase接口
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

namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 同步基元接口：提供供 lock 使用的同步对象
    /// </summary>
    public interface ISyncBase
    {
        /// <summary>
        /// 同步对象，用于lock
        /// </summary>
        object SyncLocker
        {
            get;
        }
    }
}