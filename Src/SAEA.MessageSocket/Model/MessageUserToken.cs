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
*命名空间：SAEA.MessageSocket.Model
*文件名： MessageUserToken
*版本号： v26.4.23.1
*唯一标识：2f24f933-c3f9-4436-b227-ff814a38d40c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：MessageUserToken令牌类
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MessageUserToken令牌类
*
*****************************************************************************/
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;
using System;

namespace SAEA.MessageSocket.Model
{
    class MessageUserToken: BaseUserToken, IUserToken
    {
        public DateTime Logined
        {
            get;set;
        }

    }
}