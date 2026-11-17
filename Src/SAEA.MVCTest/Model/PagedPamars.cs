/****************************************************************************
*Copyright (c)  yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVCTest.Model
*文件名： PagedPamars
*版本号： v7.0.0.1
*唯一标识：b20e930b-0655-4340-9bcf-bd8887c944f0
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
    /// 分页请求参数
    /// </summary>
    public class PagedPamars
    {
        /// <summary>
        /// 页码
        /// </summary>
        public int PageIndex { get; set; }

        /// <summary>
        /// 每页记录数
        /// </summary>
        public int PageSize { get; set; }
    }
}
