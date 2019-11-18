/****************************************************************************
*项目名称：SAEA.FTPTest.Model
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Model
*类 名 称：CustomerViewList
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 15:43:58
*描述：
*=====================================================================
*修改时间：2019/11/18 15:43:58
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.FTPTest.Model
{
    /// <summary>
    /// ListView视图
    /// </summary>
    public enum CustomerViewList
    {
        大图标 = 1,
        小图标 = 2,
        平铺 = 3,
        列表 = 4,
        详细信息 = 5
    }
}
