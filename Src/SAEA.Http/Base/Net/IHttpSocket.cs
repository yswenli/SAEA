/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                                
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *机器名称：WALLE
 *公司名称：yswenli
 *命名空间：SAEA.Http.Base.Net
 *文件名： IHttpSocket
 *版本号： v26.11.5.1
 *唯一标识：a4c2f0b3-9d17-4f52-8e6a-5b8d0f1c3a29
 *当前的用户域：WALLE
 *创建人： yswenli
 *电子邮箱：yswenli@outlook.com
 *创建时间：2018/10/28 14:19:50
 *描述：底层 HTTP 服务器抽象，只保留启停与空闲豁免
 *
 *=====================================================================
 *修改标记
 *修改时间：2026/11/10 9:50:00
 *修改人： yswenli
 *版本号： v26.11.5.1
 *描述：删除接收、错误与断开事件以及按令牌的收发入口，收发统一走连接管线
 *
 *****************************************************************************/
namespace SAEA.Http.Base.Net
{
    /// <summary>
    /// 底层 HTTP 服务器抽象：负责启停与连接空闲豁免。
    /// </summary>
    internal interface IHttpSocket
    {
        /// <summary>
        /// 设置/取消会话的空闲超时豁免
        /// </summary>
        /// <param name="id">会话标识</param>
        /// <param name="noTimeout">true 表示不参与空闲清理</param>
        void SetNoTimeout(string id, bool noTimeout);

        /// <summary>
        /// 启动服务器
        /// </summary>
        void Start();

        /// <summary>
        /// 停止服务器
        /// </summary>
        void Stop();
    }
}