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
*命名空间：SAEA.P2P.Builder
*文件名： LoggingOptions
*版本号： v26.11.5.1
*唯一标识：0f5754a9-e4e0-4684-b8eb-4418d85ff4cb
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/13 12:10:00
*描述：日志输出配置
*
*=====================================================================
*修改标记
*修改时间：2026/11/13 12:10:00
*修改人： yswenli
*版本号： v26.11.5.1
*描述：从 P2POptions 拆出日志输出配置
*
*****************************************************************************/
namespace SAEA.P2P.Builder
{
    /// <summary>
    /// 日志输出参数，控制开关、等级阈值与输出目标
    /// </summary>
    public class LoggingOptions
    {
        /// <summary>
        /// 是否启用日志，默认开启（当前运行时未读取该值，输出与否由等级阈值决定）
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// 日志等级阈值，默认 2；0=Trace、1=Debug、2=Info、3=Warning、4=Error，数值越大输出越少
        /// </summary>
        public int Level { get; set; } = 2;
        
        /// <summary>
        /// 日志最低等级的枚举形式，默认 Info（运行时以 Level 数值为准，该值未被读取）
        /// </summary>
        public LogLevel MinimumLevel { get; set; } = LogLevel.Info;
        
        /// <summary>
        /// 是否写入日志文件，默认关闭（当前运行时未读取该值）
        /// </summary>
        public bool LogToFile { get; set; } = false;
        
        /// <summary>
        /// 日志文件路径，默认 logs/p2p.log（当前运行时未读取该值）
        /// </summary>
        public string LogFilePath { get; set; } = "logs/p2p.log";
        
        /// <summary>
        /// 是否输出到控制台，默认开启（当前运行时未读取该值）
        /// </summary>
        public bool LogToConsole { get; set; } = true;
    }
}