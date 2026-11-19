/****************************************************************************
 *项目名称：SAEA.MVCTest.Controllers
 *CLR 版本：4.0.30319.42000
 *命名空间：SAEA.MVCTest.Controllers
 *类 名 称：EchoController
 *描述：不 override OpenAsync 的派生类，验证回落到 WSController 的默认实现
 *=====================================================================
 *****************************************************************************/
using SAEA.MVC.Controllers;

namespace SAEA.MVCTest.Controllers
{
    /// <summary>
    /// 不 override 任何生命周期方法：GET /echo/open 应回落到 WSController.OpenAsync，
    /// 完成握手后即可连接（消息不回显，行为由基类决定）
    /// </summary>
    public class EchoController : WSController
    {
    }
}