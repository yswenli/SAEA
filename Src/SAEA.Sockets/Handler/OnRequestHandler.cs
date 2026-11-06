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
*文件名： OnRequestHandler
*版本号： v26.4.23.1
*唯一标识：e0900822-bf7c-404f-86b8-a89b3bad7e55
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/03/18 02:16:04
*描述：OnRequestHandler处理类
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
    /// 文件传输请求回调，参数为发起方会话标识、文件名与文件总长度
    /// </summary>
    /// <param name="ID">会话标识</param>
    /// <param name="fileName">文件名，报文未携带时为空字符串</param>
    /// <param name="length">文件总长度（字节），未携带时为 0</param>
    public delegate void OnRequestHandler(string ID, string fileName, long length);
}