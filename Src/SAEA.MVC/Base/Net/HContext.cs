/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.MVC.Base.Net
*文件名： HContext
*版本号： V2.2.2.0
*唯一标识：5977c7e0-64a5-44d5-8931-fcaeec6b203a
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/8 17:18:42
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/8 17:18:42
*修改人： yswenli
*版本号： V2.2.2.0
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Interface;
using SAEA.Sockets.Model;

namespace SAEA.MVC.Base.Net
{
    class HContext : IContext
    {
        public IUserToken UserToken { get; set; }

        public ICoder Coder { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public HContext()
        {
            this.UserToken = new UserToken();
            this.Coder = new HCoder();
            this.UserToken.Coder = this.Coder;
        }
    }
}
