/****************************************************************************
*项目名称：SAEA.FTP.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Model
*类 名 称：ServerResponseCode
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/9/27 15:47:40
*描述：
*=====================================================================
*修改时间：2019/9/27 15:47:40
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.FTP.Model
{
    public class ServerResponseCode
    {
        public const int 新文件指示器上的重启标记 = 110;

        public const int 服务器准备就绪的时间 = 120;

        public const int 打开数据连接开始传输 = 125;

        public const int 打开连接 = 150;

        public const int 成功 = 200;

        public const int 初始命令没有执行 = 202;

        public const int 系统状态回复 = 211;

        public const int 目录状态回复 = 212;

        public const int 文件状态回复 = 213;

        public const int 帮助信息回复 = 214;

        public const int 系统类型回复 = 215;

        public const int 服务就绪 = 220;

        public const int 退出网络 = 221;

        public const int 打开数据连接 = 225;

        public const int 结束数据连接 = 226;

        public const int 进入被动模式 = 227;

        public const int 登录成功 = 230;

        public const int 文件行为完成 = 250;

        public const int 路径名建立 = 257;

        public const int 要求密码 = 331;

        public const int 要求帐号 = 332;

        public const int 文件行为暂停 = 350;

        public const int 服务关闭 = 421;

        public const int 无法打开数据连接 = 425;

        public const int 结束连接 = 426;

        public const int 文件不可用 = 450;

        public const int 遇到本地错误 = 451;

        public const int 磁盘空间不足 = 452;

        public const int 无效命令 = 500;

        public const int 错误参数 = 501;

        public const int 命令没有执行 = 502;

        public const int 错误指令序列 = 503;

        public const int 无效命令参数 = 504;

        public const int 禁用 = 510;

        public const int 无效的用户名 = 530;

        public const int 存储文件需要帐号 = 532;

        public const int 找不到文件或文件夹 = 550;

        public const int 输出文件出错 = 551;

        public const int 超过存储分配 = 552;

        public const int 文件名不允许 = 553;


    }
}
