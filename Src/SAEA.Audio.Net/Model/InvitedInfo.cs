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
*命名空间：SAEA.Audio.Model
*文件名： InvitedInfo
*版本号： v26.4.23.1
*唯一标识：28148dd6-c534-4f8e-9e3f-7b72860e1cbd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/02/10 15:16:38
*描述：InvitedInfo接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:33
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.Audio.Model
{
    /// <summary>
    /// 邀请信息
    /// </summary>
    public class InvitedInfo
    {
        /// <summary>
        /// 被邀请方的用户标识
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// 邀请加入的频道标识
        /// </summary>
        public string ChannelID { get; set; }

    }
}
