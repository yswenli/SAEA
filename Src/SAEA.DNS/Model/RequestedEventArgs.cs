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
*命名空间：SAEA.DNS.Model
*文件名： RequestedEventArgs
*版本号： v26.4.23.1
*唯一标识：57756ed0-9e80-4a1f-8d15-ee4170b6eaff
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 22:58:12
*描述：RequestedEventArgs事件参数类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Protocol;
using System;

namespace SAEA.DNS.Model
{
    /// <summary>
    /// DNS 请求事件参数
    /// </summary>
    public class RequestedEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化请求事件参数
        /// </summary>
        /// <param name="request">解析后的请求</param>
        /// <param name="data">请求原始字节</param>
        public RequestedEventArgs(IRequest request, byte[] data)
        {
            Request = request;
            Data = data;
        }

        /// <summary>
        /// 解析后的请求
        /// </summary>
        public IRequest Request
        {
            get;
            private set;
        }

        /// <summary>
        /// 请求原始字节
        /// </summary>
        public byte[] Data
        {
            get;
            private set;
        }
    }
}
