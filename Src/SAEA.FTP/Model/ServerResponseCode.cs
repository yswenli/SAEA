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
*命名空间：SAEA.FTP.Model
*文件名： ServerResponseCode
*版本号： v26.4.23.1
*唯一标识：40fd51ef-064c-4514-9f46-47f82e619ed2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/09/27 17:50:27
*描述：ServerResponseCode代码枚举
*
*=====================================================================
*修改标记
*修改时间：2026/11/6 12:52:28
*修改人： yswenli
*版本号： v26.11.5.1
*描述：补全公开成员的 XML 注释
*
*****************************************************************************/
namespace SAEA.FTP.Model
{
    /// <summary>
    /// FTP 响应码常量，对应 RFC 959 定义的三位状态码
    /// </summary>
    public class ServerResponseCode
    {
        /// <summary>
        /// 110：重启标记回复，表示可在指定偏移处继续传输
        /// </summary>
        public const int 新文件指示器上的重启标记 = 110;

        /// <summary>
        /// 120：服务已就绪，将在指定时间后可用
        /// </summary>
        public const int 服务器准备就绪的时间 = 120;

        /// <summary>
        /// 125：数据连接已打开，传输即将开始
        /// </summary>
        public const int 打开数据连接开始传输 = 125;

        /// <summary>
        /// 150：文件状态正常，即将打开数据连接
        /// </summary>
        public const int 打开连接 = 150;

        /// <summary>
        /// 200：命令执行成功
        /// </summary>
        public const int 成功 = 200;

        /// <summary>
        /// 202：命令未执行，该命令在此处多余
        /// </summary>
        public const int 初始命令没有执行 = 202;

        /// <summary>
        /// 211：系统状态或系统帮助回复
        /// </summary>
        public const int 系统状态回复 = 211;

        /// <summary>
        /// 212：目录状态回复
        /// </summary>
        public const int 目录状态回复 = 212;

        /// <summary>
        /// 213：文件状态回复，SIZE 命令的长度结果也用它返回
        /// </summary>
        public const int 文件状态回复 = 213;

        /// <summary>
        /// 214：帮助信息回复
        /// </summary>
        public const int 帮助信息回复 = 214;

        /// <summary>
        /// 215：系统类型回复
        /// </summary>
        public const int 系统类型回复 = 215;

        /// <summary>
        /// 220：服务就绪，等待新用户登录
        /// </summary>
        public const int 服务就绪 = 220;

        /// <summary>
        /// 221：正在关闭控制连接，退出登录
        /// </summary>
        public const int 退出网络 = 221;

        /// <summary>
        /// 225：数据连接已打开，当前没有传输进行
        /// </summary>
        public const int 打开数据连接 = 225;

        /// <summary>
        /// 226：数据连接已关闭，请求的文件操作成功
        /// </summary>
        public const int 结束数据连接 = 226;

        /// <summary>
        /// 227：进入被动模式，响应内含数据连接的地址与端口
        /// </summary>
        public const int 进入被动模式 = 227;

        /// <summary>
        /// 229：进入扩展被动模式，响应内含数据连接端口
        /// </summary>
        public const int 进入扩展被动模式 = 229;

        /// <summary>
        /// 230：登录成功，可以继续后续命令
        /// </summary>
        public const int 登录成功 = 230;

        /// <summary>
        /// 250：请求的文件操作已完成
        /// </summary>
        public const int 文件行为完成 = 250;

        /// <summary>
        /// 257：路径名已建立，响应内含双引号包裹的目录名
        /// </summary>
        public const int 路径名建立 = 257;

        /// <summary>
        /// 331：用户名正确，还需要提供密码
        /// </summary>
        public const int 要求密码 = 331;

        /// <summary>
        /// 332：登录还需提供帐号信息
        /// </summary>
        public const int 要求帐号 = 332;

        /// <summary>
        /// 350：请求的操作等待进一步信息，如 RNFR 之后等待 RNTO
        /// </summary>
        public const int 文件行为暂停 = 350;

        /// <summary>
        /// 421：服务不可用，正在关闭控制连接；本实现用于超出单账号并发会话数
        /// </summary>
        public const int 服务关闭 = 421;

        /// <summary>
        /// 425：无法打开数据连接
        /// </summary>
        public const int 无法打开数据连接 = 425;

        /// <summary>
        /// 426：连接被关闭，传输中止
        /// </summary>
        public const int 结束连接 = 426;

        /// <summary>
        /// 450：文件不可用，请求的操作未执行
        /// </summary>
        public const int 文件不可用 = 450;

        /// <summary>
        /// 451：请求中止，处理过程中出现本地错误
        /// </summary>
        public const int 遇到本地错误 = 451;

        /// <summary>
        /// 452：请求未执行，存储空间不足
        /// </summary>
        public const int 磁盘空间不足 = 452;

        /// <summary>
        /// 500：语法错误，命令无法识别
        /// </summary>
        public const int 无效命令 = 500;

        /// <summary>
        /// 501：参数或语法错误
        /// </summary>
        public const int 错误参数 = 501;

        /// <summary>
        /// 502：命令未实现
        /// </summary>
        public const int 命令没有执行 = 502;

        /// <summary>
        /// 503：命令顺序错误，如未登录就发送需认证的命令
        /// </summary>
        public const int 错误指令序列 = 503;

        /// <summary>
        /// 504：该命令不支持此参数
        /// </summary>
        public const int 无效命令参数 = 504;

        /// <summary>
        /// 510：非标准响应码，表示相应功能被禁用
        /// </summary>
        public const int 禁用 = 510;

        /// <summary>
        /// 530：未登录或用户名密码错误，本实现用于登录失败
        /// </summary>
        public const int 无效的用户名 = 530;

        /// <summary>
        /// 532：存储文件需要帐号信息
        /// </summary>
        public const int 存储文件需要帐号 = 532;

        /// <summary>
        /// 550：找不到文件或目录，或无权访问
        /// </summary>
        public const int 找不到文件或文件夹 = 550;

        /// <summary>
        /// 551：输出文件出错，如页类型未知
        /// </summary>
        public const int 输出文件出错 = 551;

        /// <summary>
        /// 552：超出存储分配额度
        /// </summary>
        public const int 超过存储分配 = 552;

        /// <summary>
        /// 553：文件名不被允许
        /// </summary>
        public const int 文件名不允许 = 553;


    }
}
