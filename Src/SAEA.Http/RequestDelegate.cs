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
*命名空间：SAEA.Http
*文件名： RequestDelegate
*版本号： v26.4.23.1
*唯一标识：fea992f9-9f8e-47f6-b29a-79054ca6ede2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/08/22 10:51:01
*描述：RequestDelegate委托类
*
*=====================================================================
*修改标记
*修改时间：2020/08/22 10:51:01
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RequestDelegate委托类
*
*****************************************************************************/
using SAEA.Http.Model;
using System.Threading.Tasks;

namespace SAEA.Http
{
    /// <summary>
    /// 委托处理
    /// </summary>
    /// <param name="context"></param>
    /// <returns></returns>
    public delegate void RequestDelegate(IHttpContext context);
}
