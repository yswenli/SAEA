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
*命名空间：SAEA.DNS.Coder
*文件名： NullRequestCoder
*版本号： v26.4.23.1
*唯一标识：a506345d-2639-4f97-b435-95624c40d7ae
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/29 20:45:22
*描述：NullRequestCoder编解码类
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:47
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
using SAEA.DNS.Model;
using SAEA.DNS.Protocol;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.DNS.Coder
{
    /// <summary>
    /// 空处理编解码器，不发送任何数据
    /// </summary>
    public class NullRequestCoder : IRequestCoder
    {
        /// <summary>
        /// 直接抛出响应异常，表示该请求无法处理
        /// </summary>
        /// <param name="request">待处理的 DNS 请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>不会返回，始终抛出 ResponseException</returns>
        public Task<IResponse> Code(IRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new ResponseException("Request failed");
        }
    }
}
