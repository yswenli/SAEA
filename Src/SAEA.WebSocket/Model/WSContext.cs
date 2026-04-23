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
*命名空间：SAEA.WebSocket.Model
*文件名： WSContext
*版本号： v26.4.23.1
*唯一标识：bb2af5d9-5866-470d-8ce4-ba4b9dff7d2d
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Sockets.Base;
using SAEA.Sockets.Interface;

namespace SAEA.WebSocket.Model
{
    class WSContext : BaseContext<WSCoder>
    {
        public override IUserToken UserToken { get; set; }

        public override ICoder Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public WSContext()
        {
            this.UserToken = new WSUserToken();
            this.Unpacker = new WSCoder();
            this.UserToken.Coder = this.Unpacker;
        }
    }
}