/****************************************************************************
*项目名称：SAEA.Audio.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Audio.Model
*类 名 称：ProtocalType
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/2/10 9:51:56
*描述：
*=====================================================================
*修改时间：2021/2/10 9:51:56
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

namespace SAEA.Audio.Model
{
    public enum ProtocalType : byte
    {
        Ping = 0,
        Pong = 1,

        /// <summary>
        /// 邀请
        /// </summary>
        Invite = 2,

        /// <summary>
        /// 同意
        /// </summary>
        Agree = 3,
        /// <summary>
        /// 拒绝
        /// </summary>
        Disagree = 4,
        /// <summary>
        /// 数据
        /// </summary>
        Data = 5,
        /// <summary>
        /// 加入
        /// </summary>
        Join = 6,
        /// <summary>
        /// 退出
        /// </summary>
        Quit = 7
    }
}
