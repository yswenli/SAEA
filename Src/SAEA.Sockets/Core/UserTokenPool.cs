/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenPool
*版本号： V3.1.1.0
*唯一标识：ef84e44b-6fa2-432e-90a2-003ebd059303
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/10/26 15:54:21
*描述：
*
*=====================================================================
*修改标记
*创建时间：2018/10/26 15:54:21
*修改人： yswenli
*版本号： V3.1.1.0
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;

namespace SAEA.Sockets.Core
{
    public class UserTokenPool : IDisposable
    {
        ConcurrentQueue<IUserToken> concurrentQueue = new ConcurrentQueue<IUserToken>();

        object locker = new object();

        public UserTokenPool(IContext context, int count)
        {

            for (int i = 0; i < count; i++)
            {
                IUserToken userToken = UserTokenFactory.Create(context);
                concurrentQueue.Enqueue(userToken);
            }
        }

        public IUserToken Dequeue()
        {
            if (concurrentQueue.TryDequeue(out IUserToken userToken))
            {
                return userToken;
            }
            else
            {
                ThreadHelper.Sleep(1);
                return Dequeue();
            }
                
        }

        public void Enqueue(IUserToken userToken)
        {
            userToken.Socket = null;
            userToken.ReadArgs = null;
            userToken.WriteArgs = null;
            concurrentQueue.Enqueue(userToken);
        }

        public void Dispose()
        {
            concurrentQueue.Clear();
        }
    }
}
