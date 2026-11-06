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
*版本号： v7.0.0.1
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
        /// <summary>
        /// 大图标视图，每项以 32x32 图标显示
        /// </summary>
        大图标 = 1,
        /// <summary>
        /// 小图标视图，每项以 20x20 图标显示
        /// </summary>
        小图标 = 2,
        /// <summary>
        /// 平铺视图，图标与说明文字横向排布
        /// </summary>
        平铺 = 3,
        /// <summary>
        /// 列表视图，单列纵向排列
        /// </summary>
        列表 = 4,
        /// <summary>
        /// 详细信息视图，按列显示名称、类型、大小等属性
        /// </summary>
        详细信息 = 5
    }
}
