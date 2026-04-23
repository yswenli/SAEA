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
*命名空间：SAEA.Http.Model
*文件名： HttpEmptyResult
*版本号： v26.4.23.1
*唯一标识：ad01182d-7fa2-4d19-9ff1-806b41bde94f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/29 15:01:17
*描述：HttpEmptyResult结果类
*
*=====================================================================
*修改标记
*修改时间：2018/10/29 15:01:17
*修改人： yswenli
*版本号： v26.4.23.1
*描述：HttpEmptyResult结果类
*
*****************************************************************************/
namespace SAEA.Http.Model
{
    /// <summary>
    /// 空结果
    /// 用于直接的response操作场景
    /// </summary>
    public class HttpEmptyResult : HttpActionResult, IEmptyResult
    {

    }
}