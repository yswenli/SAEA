/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                             

*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 2.1.4
*机器名称：WENLI-PC
*公司名称：wenli
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenFactory
*版本号： v4.3.3.7
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
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace SAEA.Sockets.Core
{
    /// <summary>
    /// 创建IUserToken
    /// </summary>
    public class UserTokenFactory
    {
        ConcurrentDictionary<Type, TypeInfo> _concurrentDictionary = new ConcurrentDictionary<Type, TypeInfo>();

        /// <summary>
        /// 生成基本的IUserToken
        /// </summary>
        /// <returns></returns>
        public IUserToken Create(IContext context)
        {
            var type = context.GetType();
            var typeInfo = _concurrentDictionary.GetOrAdd(type, (k) =>
            {
                return new TypeInfo()
                {
                    UserTokenType = context.UserToken.GetType(),
                    UnpackerType= context.UserToken.Unpacker.GetType()
                };
            });
            IUserToken userToken = (IUserToken)Activator.CreateInstance(typeInfo.UserTokenType);
            IUnpacker unpakcer = (IUnpacker)Activator.CreateInstance(typeInfo.UnpackerType);
            userToken.Unpacker = unpakcer;
            return userToken;
        }

        /// <summary>
        /// 生成初始化args的IUserToken
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bufferSize"></param>
        /// <param name="eventHandler"></param>
        /// <returns></returns>
        public IUserToken Create(IContext context,int bufferSize, EventHandler<SocketAsyncEventArgs> eventHandler)
        {
            IUserToken userToken = Create(context);

            userToken.ReadArgs = new SocketAsyncEventArgs();
            userToken.ReadArgs.Completed += eventHandler;
            userToken.ReadArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
            userToken.WriteArgs = new SocketAsyncEventArgs();
            userToken.WriteArgs.Completed += eventHandler;
            userToken.ReadArgs.UserToken = userToken.WriteArgs.UserToken = userToken;

            return userToken;
        }

        class TypeInfo
        {
            public Type UserTokenType { get; set; }

            public Type UnpackerType { get; set; }
        }
    }
}
