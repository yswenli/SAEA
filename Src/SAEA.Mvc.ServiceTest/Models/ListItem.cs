/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Models
*类 名 称：ListItem
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

namespace SAEA.RESTED.Models
{
    public class ListItem
    {
        public string ID
        {
            get; set;
        }

        public string Title
        {
            get;set;
        }
        public string Url
        {
            get; set;
        }

        public string Method
        {
            get; set;
        }

        public string RequestJson
        {
            get; set;
        }
    }
}
