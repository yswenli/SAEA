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
*命名空间：SAEA.Sockets.Interface
*文件名： IContext
*版本号： v26.4.23.1
*唯一标识：a0835d4a-30a1-49b1-8a76-e9a2f664432f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：IContext接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.Sockets.Interface
{
    /// <summary>
    /// 编解码会话上下文：把用户令牌与编解码器绑定在一起
    /// </summary>
    /// <typeparam name="Coder">编解码器类型，构造上下文时按该类型反射创建实例</typeparam>
    public interface IContext<out Coder> where Coder : class, ICoder
    {
        /// <summary>
        /// 连接的用户令牌，承载该连接的套接字、收发参数与编解码器
        /// </summary>
        IUserToken UserToken { get; set; }

        /// <summary>
        /// 编解码器实例；实现类通常与 UserToken.Coder 指向同一对象
        /// </summary>
        ICoder Unpacker { get; set; }
    }
}