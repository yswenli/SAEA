/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Net
*文件名： RContext
*版本号： V3.1.0.0
*唯一标识：5708fa45-4a1b-4bc5-9ce1-9490eb6e34ad
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/16 16:18:34
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/16 16:18:34
*修改人： yswenli
*版本号： V3.1.0.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Net
{
    class RContext : IContext
    {
        public IUserToken UserToken { get; set; }

        public IUnpacker Unpacker { get; set; }
        /// <summary>
        /// 上下文
        /// </summary>
        public RContext()
        {
            this.UserToken = new UserToken();
            this.Unpacker = new RCoder();
            this.UserToken.Unpacker = this.Unpacker;
        }
    }
}
