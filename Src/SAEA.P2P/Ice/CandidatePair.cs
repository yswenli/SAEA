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
*修改时间：2026/11/12 14:30:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：候选对优先级支持角色切换后重算
*
*****************************************************************************/
using System;

namespace SAEA.P2P.Ice
{
    /// <summary>
    /// 候选对：本地与远端候选的组合，保存 pair priority、valid/nominated 标记与检查状态
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
        public ulong Priority { get; private set; }

        /// <summary>
        /// 是否已通过连通性检查
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// 是否已被提名
        /// </summary>
        public bool IsNominated { get; private set; }

        /// <summary>
        /// 检查状态，初始为冻结。
        /// 允许的迁移：冻结到等待、冻结或等待到检查中、检查中到成功、冻结/等待/检查中到失败、失败回到等待；
        /// 进入成功或失败后只有失败可以回到等待继续重发。
        /// </summary>
        public PairState State { get; private set; }

        /// <summary>
        /// 是否处于终态：成功或失败都不再被选择器挑出
        /// </summary>
        public bool IsTerminal
        {
            get { return State == PairState.Succeeded || State == PairState.Failed; }
        }

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
        /// 将候选对从冻结状态解冻为等待检查
        /// </summary>
        /// <exception cref="InvalidOperationException">候选对不处于冻结状态</exception>
        public void MarkWaiting()
        {
            if (State != PairState.Frozen)
            {
                throw new InvalidOperationException(string.Format("候选对只能从冻结迁移到等待，当前状态为 {0}", State));
            }
            State = PairState.Waiting;
        }

        /// <summary>
        /// 标记候选对进入检查中；检查可由冻结直接触发，因此冻结与等待都允许
        /// </summary>
        /// <exception cref="InvalidOperationException">候选对已在检查中或已进入终态</exception>
        public void MarkInProgress()
        {
            if (State != PairState.Frozen && State != PairState.Waiting)
            {
                throw new InvalidOperationException(string.Format("候选对只能从冻结或等待迁移到检查中，当前状态为 {0}", State));
            }
            State = PairState.InProgress;
        }

        /// <summary>
        /// 标记候选对检查成功
        /// </summary>
        /// <exception cref="InvalidOperationException">候选对不处于检查中</exception>
        public void MarkSucceeded()
        {
            if (State != PairState.InProgress)
            {
                throw new InvalidOperationException(string.Format("候选对只能从检查中迁移到成功，当前状态为 {0}", State));
            }
            State = PairState.Succeeded;
        }

        /// <summary>
        /// 标记候选对检查失败；成功状态不允许回退
        /// </summary>
        /// <exception cref="InvalidOperationException">候选对已成功</exception>
        public void MarkFailed()
        {
            if (State == PairState.Succeeded)
            {
                throw new InvalidOperationException(string.Format("候选对已成功，不能迁移到失败，当前状态为 {0}", State));
            }
            // 超时与错误回调可能先后报告同一候选对，重复标记失败按幂等处理
            State = PairState.Failed;
        }

        /// <summary>
        /// 把失败的候选对重置为等待检查；角色切换后该候选对需要重发检查
        /// </summary>
        /// <exception cref="InvalidOperationException">候选对不处于失败状态</exception>
        public void ResetToWaiting()
        {
            if (State != PairState.Failed)
            {
                throw new InvalidOperationException(string.Format("候选对只能从失败重置为等待，当前状态为 {0}", State));
            }
            // 角色切换会更换检查角色，该候选对重新回到等待队列等待重发
            State = PairState.Waiting;
        }

        /// <summary>
        /// 按当前角色重算候选对优先级
        /// </summary>
        /// <param name="isControlling">本地是否为控制方，决定公式里 G/D 的取值方向</param>
        public void RecomputePriority(bool isControlling)
        {
            // 角色切换后对优先级的方向也跟着变，需要重算；两端候选优先级本身不变，只换 G/D 的归属
            Priority = IcePriority.ComputePairPriority(Local.Priority, Remote.Priority, isControlling);
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
