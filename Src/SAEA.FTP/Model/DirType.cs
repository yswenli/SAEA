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
*命名空间：SAEA.FTP.Model
*文件名： DirType
*版本号： v26.4.23.1
*唯一标识：885ec60d-c8e8-4bd4-86d4-05d0814275b3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/08 09:18:57
*描述：DirType接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.FTP.Model
{
    /// <summary>
    /// 目录列表命令类型
    /// </summary>
    public enum DirType
    {
        /// <summary>
        /// LIST，Unix 风格详细列表
        /// </summary>
        LIST = 1,
        /// <summary>
        /// MLSD，机器可读的目录列表
        /// </summary>
        MLSD = 2,
        /// <summary>
        /// NLST，仅返回名称列表
        /// </summary>
        NLST = 3
    }
}
