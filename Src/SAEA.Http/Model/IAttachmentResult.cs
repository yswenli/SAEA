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
*命名空间：SAEA.Http.Model
*文件名： IAttachmentResult
*版本号： v26.11.26.1
*唯一标识：38685ab5-f838-485a-9ffc-bf7b2518c089
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/26 17:46:21
*描述：附件下载结果标记
*
*=====================================================================
*修改标记
*修改时间：2026/11/26 17:46:21
*修改人： yswenli
*版本号： v26.11.26.1
*描述：新增，响应写出时据此生成 Content-Disposition
*
*****************************************************************************/
namespace SAEA.Http.Model
{
    /// <summary>
    /// 附件下载结果标记，响应写出时据此生成 Content-Disposition
    /// </summary>
    public interface IAttachmentResult
    {
        /// <summary>
        /// 下载文件名；为 null 或空白表示按普通响应输出，不写 Content-Disposition
        /// </summary>
        string FileName { get; }
    }
}