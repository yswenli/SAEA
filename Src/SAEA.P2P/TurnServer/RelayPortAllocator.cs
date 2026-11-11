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
*命名空间：SAEA.P2P.TurnServer
*文件名： RelayPortAllocator
*版本号： v26.11.5.1
*唯一标识：470d36a0-5091-412d-a27c-4641b078f01b
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/12 00:20:00
*描述：TURN 中继端口随机分配
*
*=====================================================================
*修改标记
*修改时间：2026/11/12 00:20:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：新增 TURN 中继端口随机分配
*
*****************************************************************************/
using System;
using System.Collections.Generic;

namespace SAEA.P2P.TurnServer
{
    /// <summary>
    /// TURN 中继端口分配器：在配置区间内随机起点线性探测，符合 RFC 6056 的不可预测要求。
    /// 只记录本进程已分出的端口，端口是否被系统占用由调用方绑定失败后回退处理。
    /// </summary>
    public sealed class RelayPortAllocator
    {
        /// <summary>
        /// 可用端口下界
        /// </summary>
        private readonly int _minPort;

        /// <summary>
        /// 可用端口上界
        /// </summary>
        private readonly int _maxPort;

        /// <summary>
        /// 已分出但尚未归还的端口
        /// </summary>
        private readonly HashSet<int> _usedPorts = new HashSet<int>();

        /// <summary>
        /// 保护已用端口集合的同步对象
        /// </summary>
        private readonly object _sync = new object();

        /// <summary>
        /// 随机源；注入固定种子可让用例可复现
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// 创建端口分配器
        /// </summary>
        /// <param name="minPort">可用端口下界，取值 1 到 65535</param>
        /// <param name="maxPort">可用端口上界，取值 1 到 65535 且不小于下界</param>
        /// <param name="random">随机源；为 null 时新建</param>
        /// <exception cref="ArgumentOutOfRangeException">端口区间不合法</exception>
        public RelayPortAllocator(int minPort, int maxPort, Random random = null)
        {
            if (minPort < 1 || minPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(minPort), "端口下界必须落在 1 到 65535 之间");
            }
            if (maxPort < minPort || maxPort > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPort), "端口上界必须落在下界到 65535 之间");
            }

            _minPort = minPort;
            _maxPort = maxPort;
            _random = random ?? new Random();
        }

        /// <summary>
        /// 可用端口下界
        /// </summary>
        public int MinPort
        {
            get { return _minPort; }
        }

        /// <summary>
        /// 可用端口上界
        /// </summary>
        public int MaxPort
        {
            get { return _maxPort; }
        }

        /// <summary>
        /// 尚未分出的端口数量
        /// </summary>
        public int AvailableCount
        {
            get
            {
                lock (_sync)
                {
                    return (_maxPort - _minPort + 1) - _usedPorts.Count;
                }
            }
        }

        /// <summary>
        /// 随机分配一个端口；区间耗尽时返回 false
        /// </summary>
        /// <param name="port">分出的端口；失败时为 0</param>
        /// <returns>分配成功返回 true</returns>
        public bool TryAllocate(out int port)
        {
            port = 0;
            lock (_sync)
            {
                var count = _maxPort - _minPort + 1;
                if (_usedPorts.Count >= count)
                {
                    return false;
                }

                var start = _random.Next(count);
                for (var i = 0; i < count; i++)
                {
                    var candidate = _minPort + ((start + i) % count);
                    if (_usedPorts.Add(candidate))
                    {
                        port = candidate;
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 归还端口；未分出过的端口忽略
        /// </summary>
        /// <param name="port">端口</param>
        public void Release(int port)
        {
            lock (_sync)
            {
                _usedPorts.Remove(port);
            }
        }
    }
}