/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \  ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                               
  
 *Copyright (c) yswenli All Rights Reserved.
 *CLR版本： netstandard2.0
 *公司名称：yswenli
 *命名空间：SAEA.FTP
 *文件名： FTPTransferOptions
 *描述：FTP传输可靠性、冲突处理与并行传输模型
 *
 *****************************************************************************/
using System;

namespace SAEA.FTP
{
    /// <summary>
    /// 传输方向
    /// </summary>
    public enum FTPTransferDirection
    {
        Upload,
        Download
    }

    /// <summary>
    /// 同名冲突处理动作
    /// </summary>
    public enum FTPConflictAction
    {
        /// <summary>
        /// 覆盖目标
        /// </summary>
        Overwrite,
        /// <summary>
        /// 跳过，不做传输
        /// </summary>
        Skip,
        /// <summary>
        /// 改名后传输
        /// </summary>
        Rename
    }

    /// <summary>
    /// 传输结果状态
    /// </summary>
    public enum FTPTransferStatus
    {
        Completed,
        Skipped,
        Renamed,
        Failed
    }

    /// <summary>
    /// 冲突上下文，交给调用方决定如何处理同名文件
    /// </summary>
    public sealed class FTPConflictContext
    {
        public FTPTransferDirection Direction { get; set; }

        public string LocalPath { get; set; }

        public string RemotePath { get; set; }

        /// <summary>
        /// 目标已确定存在
        /// </summary>
        public bool TargetExists { get; set; }

        /// <summary>
        /// 服务器不支持 SIZE/MLSD 等探测命令，无法确认是否存在
        /// </summary>
        public bool ExistsUnknown { get; set; }

        public long? LocalSize { get; set; }

        public long? RemoteSize { get; set; }

        public DateTime? RemoteModified { get; set; }

        /// <summary>
        /// 待传输数据大小，未知时为 0
        /// </summary>
        public long IncomingSize { get; set; }
    }

    /// <summary>
    /// 冲突处理结果
    /// </summary>
    public sealed class FTPConflictResult
    {
        public FTPConflictAction Action { get; set; } = FTPConflictAction.Skip;

        /// <summary>
        /// Action 为 Rename 时可指定新文件名；为空则按 RenamePattern 自动生成
        /// </summary>
        public string NewName { get; set; }
    }

    /// <summary>
    /// 传输结果
    /// </summary>
    public sealed class FTPTransferResult
    {
        public FTPTransferStatus Status { get; set; }

        public string LocalPath { get; set; }

        public string RemotePath { get; set; }

        public long BytesTransferred { get; set; }

        /// <summary>
        /// 单连接主流程尝试次数（并行时取分片最大值）
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// 实际使用的并行数据连接数
        /// </summary>
        public int SessionsUsed { get; set; }

        /// <summary>
        /// 是否发生了断点续传
        /// </summary>
        public bool Resumed { get; set; }

        public FTPConflictAction? ConflictAction { get; set; }

        public Exception Error { get; set; }
    }

    /// <summary>
    /// 传输选项
    /// </summary>
    public sealed class FTPTransferOptions
    {
        /// <summary>
        /// 是否允许大文件并行传输
        /// </summary>
        public bool EnableParallel { get; set; } = true;

        /// <summary>
        /// 并行数据连接数上限（包含主连接）
        /// </summary>
        public int MaxSessions { get; set; } = 4;

        /// <summary>
        /// 达到该大小才启用并行
        /// </summary>
        public long ParallelThresholdBytes { get; set; } = 8L * 1024 * 1024;

        /// <summary>
        /// 单个分片的最小长度
        /// </summary>
        public long MinSegmentSize { get; set; } = 1L * 1024 * 1024;

        /// <summary>
        /// 传输缓冲区大小；0 表示使用 ClientConfig.BufferSize
        /// </summary>
        public int BufferSize { get; set; } = 0;

        public int MinBufferSize { get; set; } = 16 * 1024;

        public int MaxBufferSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// 根据实时吞吐自动放大/缩小缓冲区
        /// </summary>
        public bool EnableAdaptiveBuffer { get; set; } = true;

        /// <summary>
        /// 每个分片/单连接最多重试次数（不含首次）
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// 重试退避基数
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// 同一次调用内允许断点续传
        /// </summary>
        public bool EnableResume { get; set; } = true;

        /// <summary>
        /// 跨调用/进程重启续传（使用确定性的 .saea-part 文件）
        /// </summary>
        public bool EnableResumeAcrossCalls { get; set; } = false;

        /// <summary>
        /// 最终失败时保留临时文件
        /// </summary>
        public bool KeepPartialOnFailure { get; set; } = true;

        /// <summary>
        /// 没有回调时的默认冲突动作；默认跳过，绝不静默覆盖
        /// </summary>
        public FTPConflictAction DefaultConflictAction { get; set; } = FTPConflictAction.Skip;

        /// <summary>
        /// 自动改名模板，占位符：{name}、{index}、{ext}
        /// </summary>
        public string RenamePattern { get; set; } = "{name} ({index}){ext}";

        /// <summary>
        /// 冲突处理回调，每个逻辑文件只调用一次
        /// </summary>
        public Func<FTPConflictContext, FTPConflictResult> ConflictHandler { get; set; }

        /// <summary>
        /// 命令超时；Zero 表示使用 ClientConfig.CommandTimeout
        /// </summary>
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// 数据连接超时；Zero 表示使用 ClientConfig.DataTimeout
        /// </summary>
        public TimeSpan DataTimeout { get; set; } = TimeSpan.Zero;

        internal int ResolveBufferSize(ClientConfig config)
        {
            var size = BufferSize > 0 ? BufferSize : (config != null ? config.BufferSize : 64 * 1024);
            if (size < MinBufferSize) size = MinBufferSize;
            if (size > MaxBufferSize) size = MaxBufferSize;
            return size;
        }

        internal TimeSpan ResolveCommandTimeout(ClientConfig config)
        {
            if (CommandTimeout > TimeSpan.Zero) return CommandTimeout;
            return config != null ? config.CommandTimeout : TimeSpan.FromSeconds(30);
        }

        internal TimeSpan ResolveDataTimeout(ClientConfig config)
        {
            if (DataTimeout > TimeSpan.Zero) return DataTimeout;
            return config != null ? config.DataTimeout : TimeSpan.FromSeconds(60);
        }
    }
}