/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Models
*类 名 称：ListItem
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/3/6 11:04:35
*描述：
*=====================================================================
*修改时间：2019/3/6 11:04:35
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

namespace SAEA.RESTED.Models
{
    /// <summary>
    /// 接口条目，记录单个接口的标题、地址、请求方法与示例请求体
    /// </summary>
    public class ListItem
    {
        /// <summary>
        /// 条目编号，由 Guid 生成的 32 位字符串
        /// </summary>
        public string ID
        {
            get; set;
        }

        /// <summary>
        /// 接口标题
        /// </summary>
        public string Title
        {
            get;set;
        }
        /// <summary>
        /// 接口地址
        /// </summary>
        public string Url
        {
            get; set;
        }

        /// <summary>
        /// 请求方法，如 GET、POST
        /// </summary>
        public string Method
        {
            get; set;
        }

        /// <summary>
        /// 示例请求体 JSON，可为空
        /// </summary>
        public string RequestJson
        {
            get; set;
        }
    }
}
