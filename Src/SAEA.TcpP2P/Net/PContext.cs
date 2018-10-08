/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.TcpP2P.Net
*文件名： QContext
*版本号： V2.1.5.0
*唯一标识：cad46d37-3703-4ffa-a721-eee312ed3eeb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/1 21:29:36
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/1 21:29:36
*修改人： yswenli
*版本号： V2.1.5.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.TcpP2P.Net
{
    public class PContext : IContext
    {
        public IUserToken UserToken { get; set; }

        public ICoder Coder { get; set; }

        /// <summary>
        /// 上下文
        /// 支持IContext 扩展
        /// </summary>
        public PContext()
        {
            this.UserToken = new PUserToken();
            this.Coder = new PCoder();
            this.UserToken.Coder = this.Coder;
        }
    }
}
