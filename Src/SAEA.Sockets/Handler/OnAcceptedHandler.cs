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
*命名空间：SAEA.Sockets.Handler
*文件名： OnAcceptedHandler
*版本号： v26.4.23.1
*唯一标识：dd2815c5-9e11-4e6f-8ff7-07dca18ff407
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：OnAcceptedHandler处理类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:27
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.Sockets.Handler
{
    /// <summary>
    /// 客户端连接建立后触发，参数为通道对象（流模式为 ChannelInfo，事件模式为 IUserToken）
    /// </summary>
    /// <param name="obj">承载连接的对象，流模式为 ChannelInfo，事件模式为 IUserToken</param>
    public delegate void OnAcceptedHandler(object obj);
}