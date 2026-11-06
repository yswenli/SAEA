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
*命名空间：SAEA.RedisSocket.Model
*文件名： RedisServerInfo
*版本号： v26.4.23.1
*唯一标识：6849fcd6-bfda-4dcc-9e8d-c25607316ebd
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/10/13 16:01:17
*描述：RedisServerInfo接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:40:46
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.RedisSocket.Model
{
    /// <summary>
    /// redis 服务器信息
    /// </summary>
    public class RedisServerInfo
    {
        /// <summary>
        /// 服务器地址，格式为 ip:port
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// 已使用的 CPU 时间，单位秒
        /// </summary>
        public double Cpu { get; set; }

        /// <summary>
        /// 已使用的内存，单位 MB
        /// </summary>
        public double Memory { get; set; }

        /// <summary>
        /// 每秒执行的命令数
        /// </summary>
        public long Cmds { get; set; }

        /// <summary>
        /// 每秒入流量，单位 KB
        /// </summary>
        public double Input { get; set; }

        /// <summary>
        /// 每秒出流量，单位 KB
        /// </summary>
        public double Output { get; set; }

        /// <summary>
        /// 当前已连接的客户端数
        /// </summary>
        public int Clients { get; set; }
    }
}
