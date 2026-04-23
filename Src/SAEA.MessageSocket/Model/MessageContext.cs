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
*命名空间：SAEA.MessageSocket.Model
*文件名： MessageContext
*版本号： v26.4.23.1
*唯一标识：930fd499-43ce-4bcf-ba7d-7003d8bd23d4
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

namespace SAEA.MessageSocket.Model
{
    public class MessageContext : BaseContext<BaseCoder>
    {
        public override IUserToken UserToken { get; set; }

        public override ICoder Unpacker { get; set; }

        /// <summary>
        /// 上下文
        /// </summary>
        public MessageContext()
        {
            this.UserToken = new MessageUserToken();
            this.Unpacker = new BaseCoder();
            this.UserToken.Coder = this.Unpacker;
        }
    }
}