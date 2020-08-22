/****************************************************************************
*项目名称：SAEA.Http
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Http
*类 名 称：RequestDelegate
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/8/22 9:24:34
*描述：
*=====================================================================
*修改时间：2020/8/22 9:24:34
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
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
