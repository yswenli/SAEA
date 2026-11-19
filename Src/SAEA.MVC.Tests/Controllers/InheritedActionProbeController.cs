/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： net10.0
*机器名称：WALLE
*公司名称：yswenli
*命名空间：SAEA.MVC.Tests.Controllers
*文件名： InheritedActionProbeController
*版本号： net10.0
*唯一标识：a32a12b7-783d-4f09-a83d-d53605e176b7
*当前的用户域：WALLE
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2026/11/19 15:13:26
*描述：基类 action 可见性用例的派生控制器
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 15:13:26
*修改人： yswenli
*版本号： net10.0
*描述：新增，只声明自有 Own 动作，不重写基类的 Inherited
*
*****************************************************************************/
namespace SAEA.MVC.Tests.Controllers
{
    /// <summary>
    /// 基类 action 可见性用例的派生类：自身只声明 Own，不重写基类的 Inherited。
    /// </summary>
    public class InheritedActionProbeController : InheritedActionBaseProbeController
    {
        /// <summary>
        /// 派生类自有入口。
        /// </summary>
        /// <returns>固定文本</returns>
        public string Own()
        {
            return "inherited-own";
        }
    }
}
