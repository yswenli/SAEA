/****************************************************************************
*Copyright (c) 2018-2022yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： EmptyResult
*版本号： v7.0.0.1
*唯一标识：38e71912-4264-40c4-bdb6-ae5deb592262
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 17:51:21
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 17:51:21
*修改人： yswenli
*版本号： v7.0.0.1
*描述：
*
*****************************************************************************/

using SAEA.Http.Model;

namespace SAEA.MVC
{
    /// <summary>
    /// 空结果
    /// 用于直接的response操作场景
    /// </summary>
    public class EmptyResult : ActionResult, IEmptyResult
    {
        static EmptyResult _emptyResult = new EmptyResult();
        public static EmptyResult Default
        {
            get
            {
                return _emptyResult;
            }
        }
    }
}
