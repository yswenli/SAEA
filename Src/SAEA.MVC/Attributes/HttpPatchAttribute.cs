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
*命名空间：SAEA.MVC.Attributes
*文件名： HttpPatchAttribute
*版本号： v26.11.16.1
*唯一标识：4a7d5286-d8f8-4010-b407-c06e4fa8e147
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:55:14
*描述：标记方法为PATCH
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:55:14
*修改人： yswenli
*版本号： v26.11.16.1
*描述：新增
*
*****************************************************************************/
using SAEA.Common;

namespace SAEA.MVC.Attributes
{
    /// <summary>
    /// 标记方法为PATCH
    /// </summary>
    public sealed class HttpPatchAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// 不声明路径，仅限定 PATCH
        /// </summary>
        public HttpPatchAttribute() : base(ConstHelper.PATCH, null)
        {
        }

        /// <summary>
        /// 以路由模板限定 PATCH
        /// </summary>
        /// <param name="template">路由模板，如 users/{id}</param>
        public HttpPatchAttribute(string template) : base(ConstHelper.PATCH, template)
        {
        }
    }
}