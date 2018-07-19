/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Sockets.Handler
*文件名： OnHeartHandler
*版本号： V1.0.0.0
*唯一标识：427fe09d-9436-4915-9ff5-fcd36c37612a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 17:44:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 17:44:29
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Sockets.Handler
{
    public delegate void OnHeartHandler(IUserToken userToken, DateTime dateTime);
}
