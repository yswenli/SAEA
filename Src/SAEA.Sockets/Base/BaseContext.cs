/****************************************************************************
*项目名称：SAEA.Sockets.Base
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Sockets.Base
*类 名 称：BaseContext
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/8/20 17:03:53
*描述：
*=====================================================================
*修改时间：2019/8/20 17:03:53
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

using SAEA.Sockets.Interface;

namespace SAEA.Sockets.Base
{
    public class BaseContext<Coder> : IContext<Coder> where Coder : class, IUnpacker
    {
        public virtual IUserToken UserToken { get; set; }

        public virtual IUnpacker Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public BaseContext()
        {
            this.UserToken = new BaseUserToken();
            this.Unpacker = Activator.CreateInstance<Coder>();
            this.UserToken.Unpacker = this.Unpacker;
        }
    }
}
