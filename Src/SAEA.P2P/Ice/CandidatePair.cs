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
*文件名： CandidatePair
*版本号： v26.11.5.1
*唯一标识：cde5be95-101c-49fb-b206-6f0246a01ad7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 11:00:00
*描述：ICE 候选对
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 11:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：候选对优先级改为 64 位无符号整数
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 候选对：本地与远端候选的组合，保存 pair priority 与 valid/nominated 标记。
    /// pair 状态枚举与状态机在检查表任务里加入，本类型当前只保留 valid/nominated 两个标记。
    /// </summary>
    public sealed class CandidatePair
    {
        /// <summary>
        /// 创建候选对并计算 pair priority
        /// </summary>
        /// <param name="local">本地候选</param>
        /// <param name="remote">远端候选</param>
        /// <param name="isControlling">本地是否为控制方，决定公式里 G/D 的取值方向</param>
        /// <exception cref="ArgumentNullException">本地或远端候选为空</exception>
        public CandidatePair(Candidate local, Candidate remote, bool isControlling)
        {
            if (local == null)
            {
                throw new ArgumentNullException(nameof(local));
            }
            if (remote == null)
            {
                throw new ArgumentNullException(nameof(remote));
            }

            // 配对兼容性（地址族、分量、传输与方向矩阵）不在这里判断，留给检查表构建阶段
            Local = local;
            Remote = remote;
            Priority = IcePriority.ComputePairPriority(local.Priority, remote.Priority, isControlling);
        }

        /// <summary>
        /// 本地候选
        /// </summary>
        public Candidate Local { get; }

        /// <summary>
        /// 远端候选
        /// </summary>
        public Candidate Remote { get; }

        /// <summary>
        /// 候选对优先级，按控制方与受控方方向由 G/D 公式算出
        /// </summary>
        public ulong Priority { get; }

        /// <summary>
        /// 是否已通过连通性检查
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// 是否已被提名
        /// </summary>
        public bool IsNominated { get; private set; }

        /// <summary>
        /// 标记该候选对已通过连通性检查
        /// </summary>
        public void MarkValid()
        {
            IsValid = true;
        }

        /// <summary>
        /// 标记该候选对已被提名；提名必然意味着检查已通过
        /// </summary>
        public void MarkNominated()
        {
            IsValid = true;
            IsNominated = true;
        }

        /// <summary>
        /// 输出候选对的两端端点、优先级与标记状态
        /// </summary>
        /// <returns>候选对的文本描述</returns>
        public override string ToString()
        {
            return string.Format("pair {0} -> {1} priority={2} valid={3} nominated={4}", Local.EndPoint, Remote.EndPoint, Priority, IsValid, IsNominated);
        }
    }
}
