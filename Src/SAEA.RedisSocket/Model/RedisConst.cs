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
*文件名： RedisConst
*版本号： v26.4.23.1
*唯一标识：fd58ede0-49c1-42dd-a89b-a46745e7b4a0
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/23 20:05:14
*描述：RedisConst接口
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
    /// redis 协议与命令中用到的固定字符串常量
    /// </summary>
    public class RedisConst
    {
        /// <summary>
        /// 命令执行成功的应答内容
        /// </summary>
        public const string OK = "OK";

        /// <summary>
        /// 未认证时服务端返回的错误前缀
        /// </summary>
        public const string NOAuth = "NOAUTH";

        /// <summary>
        /// 通用错误应答前缀
        /// </summary>
        public const string ErrIndex = "-ERR";

        /// <summary>
        /// INFO 命令的默认段，表示输出全部信息
        /// </summary>
        public const string All = "all";

        /// <summary>
        /// redis 协议的换行分隔符
        /// </summary>
        public const string Enter = "\r\n";

        /// <summary>
        /// 空字符串
        /// </summary>
        public const string Empty = "";

        /// <summary>
        /// SLAVEOF 取消主从复制的参数值，表示不跟随任何节点
        /// </summary>
        public const string NoOne = "NO ONE";

        /// <summary>
        /// 扫描命令的模式匹配关键字
        /// </summary>
        public const string MATCH = "MATCH";

        /// <summary>
        /// 扫描命令的返回条数关键字
        /// </summary>
        public const string COUNT = "COUNT";

        /// <summary>
        /// 主节点角色名称
        /// </summary>
        public const string Master = "Master";

        /// <summary>
        /// 从节点角色名称
        /// </summary>
        public const string Slave = "Slave";
    }
}