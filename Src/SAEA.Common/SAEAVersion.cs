/****************************************************************************
*Copyright (c) 2018-2020 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.Commom
*文件名： SAEAVersion
*版本号： v5.0.0.1
*唯一标识：bf3043aa-a84d-42ab-a6b6-b3adf2ab8925
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 16:53:26
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 16:53:26
*修改人： yswenli
*版本号： v5.0.0.1
*描述：
*
*****************************************************************************/

namespace SAEA.Common
{
    /// <summary>
    /// 版本
    /// </summary>
    public static class SAEAVersion
    {
        const string version = "v5.3.5.1";

        /// <summary>
        /// 获取版本
        /// </summary>
        /// <returns></returns>
        public static new string ToString()
        {
            return version;
        }
    }
}
