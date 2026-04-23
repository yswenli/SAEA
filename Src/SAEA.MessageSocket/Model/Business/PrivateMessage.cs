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
*命名空间：SAEA.MessageSocket.Model.Business
*文件名： PrivateMessage
*版本号： v26.4.23.1
*唯一标识：b26ff03b-728e-4579-b6dd-3c58546d82d5
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：PrivateMessage接口
*
*=====================================================================
*修改标记
*修改时间：2018/03/18 02:16:04
*修改人： yswenli
*版本号： v26.4.23.1
*描述：PrivateMessage接口
*
*****************************************************************************/
namespace SAEA.MessageSocket.Model.Business
{
    public class PrivateMessage: IMessage
    {
        public string Sender
        {
            get;set;
        }
        public string Receiver
        {
            get;set;
        }
        public string Content
        {
            get;set;
        }
        public string Sended
        {
            get;set;
        }
    }
}