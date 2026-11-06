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
*命名空间：SAEA.Sockets.Base
*文件名： BaseContext
*版本号： v26.4.23.1
*唯一标识：8fd69540-dafb-4d39-896a-b8debbfe8207
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/21 19:42:03
*描述：BaseContext上下文类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using System;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    /// <summary>
    /// 上下文基类，构造时创建默认令牌与编解码器实例，并把令牌的 Coder 指向该实例
    /// </summary>
    /// <typeparam name="Coder">编解码器实现类型，须有无参构造函数</typeparam>
    public class BaseContext<Coder> : IContext<Coder> where Coder : class, ICoder
    {
        /// <summary>
        /// 当前连接的令牌，默认在构造时创建
        /// </summary>
        public virtual IUserToken UserToken { get; set; }

        /// <summary>
        /// 编解码器实例，构造时按 Coder 反射创建，并在构造时赋给 UserToken.Coder
        /// </summary>
        public virtual ICoder Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public BaseContext()
        {
            this.UserToken = new BaseUserToken();
            this.Unpacker = Activator.CreateInstance<Coder>();
            this.UserToken.Coder = this.Unpacker;
        }
    }
}
