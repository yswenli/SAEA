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
*命名空间：SAEA.Sockets.Handler
*文件名： OnReceiveHandler
*版本号： v26.4.23.1
*唯一标识：e0cb6560-7470-4dba-b5f0-717aa7798601
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/02/11 17:03:06
*描述：OnReceiveHandler接口
*
*=====================================================================
*修改标记
*修改时间：2019/02/11 17:03:06
*修改人： yswenli
*版本号： v26.4.23.1
*描述：OnReceiveHandler接口
*
*****************************************************************************/
using System;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Handler
{
    public delegate void OnReceiveHandler(ISession currentSession, byte[] data);

    public delegate void OnClientReceiveHandler(byte[] data);
}