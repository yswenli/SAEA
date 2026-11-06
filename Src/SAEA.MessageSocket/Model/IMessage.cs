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
*文件名： IMessage
*版本号： v26.4.23.1
*唯一标识：4ad6c6c3-c18a-4497-a6a3-d0421bdd3d8b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：IMessage消息类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:20:17
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.MessageSocket.Model
{
    /// <summary>
    /// 业务消息统一接口，约定发送者、发送时间与正文
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// 发送者标识，由服务端在转发时填写
        /// </summary>
        string Sender
        {
            get; set;
        }

        /// <summary>
        /// 发送时间字符串，由服务端在转发时填写
        /// </summary>
        string Sended
        {
            get; set;
        }

        /// <summary>
        /// 消息正文
        /// </summary>
        string Content
        {
            get; set;
        }
    }
}