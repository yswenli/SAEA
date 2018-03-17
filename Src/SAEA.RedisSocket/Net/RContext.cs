/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RedisSocket.Net
*文件名： RContext
*版本号： V1.0.0.0
*唯一标识：aef671e4-d26d-47fa-ada1-fb4b7f54d75d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/3/16 10:25:38
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/3/16 10:25:38
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RedisSocket.Net
{
    /// <summary>
    /// 上下文
    /// </summary>
    public class RContext : IContext
    {
        public IUserToken UserToken { get; set; }

        public ICoder Coder { get; set; }

        /// <summary>
        /// 内置上下文
        /// 支持IContext 扩展
        /// </summary>
        public RContext()
        {
            this.UserToken = new UserToken();
            this.Coder = new RCoder();
            this.UserToken.Coder = this.Coder;
        }
    }
}
