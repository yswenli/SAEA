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
*命名空间：SAEA.Sockets.Core
*文件名： UserTokenFactory
*版本号： v26.4.23.1
*唯一标识：25369e00-a679-4c4c-9f94-0576261c5fe9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/27 18:03:54
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/10/27 18:03:54
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

using SAEA.Sockets.Interface;

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
        public IUserToken Create(IContext<ICoder> context)
        {
            var type = context.GetType();
            var typeInfo = _concurrentDictionary.GetOrAdd(type, (k) =>
            {
                return new TypeInfo()
                {
                    UserTokenType = context.UserToken.GetType(),
                    UnpackerType = context.UserToken.Coder.GetType()
                };
            });
            IUserToken userToken = (IUserToken)Activator.CreateInstance(typeInfo.UserTokenType);
            ICoder unpakcer = (ICoder)Activator.CreateInstance(typeInfo.UnpackerType);
            userToken.Coder = unpakcer;
            context.UserToken = userToken;
            return userToken;
        }

        /// <summary>
        /// 生成初始化args的IUserToken
        /// </summary>
        /// <param name="context"></param>
        /// <param name="bufferSize"></param>
        /// <param name="eventHandler"></param>
        /// <returns></returns>
        public IUserToken Create(IContext<ICoder> context, int bufferSize, EventHandler<SocketAsyncEventArgs> eventHandler)
        {
            IUserToken userToken = Create(context);

            userToken.ReadArgs = new SocketAsyncEventArgs();
            userToken.ReadArgs.Completed += eventHandler;
            // 使用共享的 BufferManager 替代 new byte[bufferSize]
            var bufferManager = BufferManager.GetOrCreate(bufferSize);
            bufferManager.SetBuffer(userToken.ReadArgs);
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