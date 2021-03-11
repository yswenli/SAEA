/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenPool
*版本号： v6.0.0.1
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
*版本号： v6.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Common;
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SAEA.Sockets.Core
{
    public class UserTokenPool : IDisposable
    {
        UserTokenFactory _userTokenFactory;

        ConcurrentQueue<IUserToken> concurrentQueue = new ConcurrentQueue<IUserToken>();


        public UserTokenPool(IContext context, int count)
        {
            _userTokenFactory = new UserTokenFactory();

            for (int i = 0; i < count; i++)
            {
                IUserToken userToken = _userTokenFactory.Create(context);
                concurrentQueue.Enqueue(userToken);
            }
        }

        public IUserToken Dequeue()
        {
            IUserToken token;

            while (!concurrentQueue.TryDequeue(out token))
            {
                Thread.Sleep(1);
            }

            return token;
        }

        public void Enqueue(IUserToken userToken)
        {
            if (userToken != null && userToken.Socket != null && userToken.ReadArgs != null && userToken.WriteArgs != null)
            {
                userToken.Socket = null;
                userToken.ReadArgs = null;
                userToken.WriteArgs = null;
                concurrentQueue.Enqueue(userToken);
            }
        }

        public void Dispose()
        {
            concurrentQueue.Clear();
        }
    }
}
