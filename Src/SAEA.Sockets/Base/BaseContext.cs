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
*唯一标识：3328fd60-6b83-41d0-b1a4-a31a72b912db
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/08/21 19:42:03
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/08/21 19:42:03
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using System;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    public class BaseContext<Coder> : IContext<Coder> where Coder : class, ICoder
    {
        public virtual IUserToken UserToken { get; set; }

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
