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
*命名空间：SAEA.WebSocket.Model
*文件名： IWSServer
*版本号： v26.4.23.1
*唯一标识：ca58f599-d0c0-4bf0-b39c-bc6d408dc45b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/06/14 11:19:42
*描述：IWSServer接口
*
*=====================================================================
*修改标记
*修改时间：2019/06/14 11:19:42
*修改人： yswenli
*版本号： v26.4.23.1
*描述：IWSServer接口
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.WebSocket.Model
{
    public interface IWSServer
    {
        event Action<string> OnConnected;

        event Action<string, WSProtocal> OnMessage;

        event Action<string> OnDisconnected;

        List<string> Clients {  set; get; }

        void Start(int backlog = 10 * 1000);
       

        void Reply(string id, WSProtocal data);


        void Disconnect(string id, WSProtocal data);


        void Disconnect(string id);

        void Stop();
    }
}