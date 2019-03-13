/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Models
*类 名 称：GroupData
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/3/6 11:04:35
*描述：
*=====================================================================
*修改时间：2019/3/6 11:04:35
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/

using System.Collections.Generic;

namespace SAEA.RESTED.Models
{
    public class GroupData
    {
        public string GroupID
        {
            get; set;
        }


        public string GroupName
        {
            get; set;
        }

        public List<ListItem> List
        {
            get; set;
        } = new List<ListItem>();
    }
}
