/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Model
*文件名： BasePamars
*版本号： v7.0.0.1
*唯一标识：a3546201-bb97-490b-bdd1-a783f215ec6b
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/11 10:45:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2026/11/17 21:00:00
*修改人： yswenli
*版本号： net462
*描述：唯一标识改为本文件专属 GUID
*
*****************************************************************************/


namespace SAEA.MVCTest.Model
{
    /// <summary>
    /// 请求公共参数，承载用户标识与登录令牌
    /// </summary>
    public class BasePamars
    {
        /// <summary>
        /// 用户 ID
        /// </summary>
        public int UID
        {
            get; set;
        }

        /// <summary>
        /// 登录令牌
        /// </summary>
        public string Token
        {
            get;
            set;
        }
    }
}
