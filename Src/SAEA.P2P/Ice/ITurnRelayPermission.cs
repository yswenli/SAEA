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
*命名空间：SAEA.P2P.Ice
*文件名： ITurnRelayPermission
*版本号： v26.11.5.1
*唯一标识：e8dc7b2b-bb34-4ccf-a990-5ec6e3f33dbb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 16:30:00
*描述：TURN 中继侧对端权限契约
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 16:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 中继侧对端权限契约
*
*****************************************************************************/
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// TURN 中继权限：从 relay 候选发起检查前，必须先为中继侧的对端地址建权限。
    /// </summary>
    public interface ITurnRelayPermission
    {
        /// <summary>
        /// 为中继侧要访问的对端地址建立权限
        /// </summary>
        /// <param name="remoteAddresses">对端候选的 IP 列表，不区分端口</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>权限建立完成的任务</returns>
        Task CreatePermissionsAsync(IReadOnlyList<IPAddress> remoteAddresses, CancellationToken cancellationToken);
    }
}
