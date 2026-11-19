/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Controllers
*类 名 称：HomeController
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
using SAEA.MVC.Controllers;
using SAEA.MVC.Hosting;
using SAEA.MVC.Results;
using SAEA.RESTED.Libs;
using SAEA.RESTED.Models;

namespace SAEA.RESTED.Controllers
{
    /// <summary>
    /// 接口文档演示站的 API 控制器，负责分组与接口条目的增删改查及导入导出
    /// </summary>
    public class HomeController : Controller
    {

        /// <summary>
        /// 输出全部分组及其接口条目的折叠面板 HTML，groupID 非空时仅展开该分组
        /// </summary>
        /// <returns>分组面板 HTML，无数据时为空字符串</returns>
        public ActionResult GetList()
        {
            string groupID = string.Empty;

            HttpContext.Current.Request.Parmas.TryGetValue("groupID", out groupID);

            return Content(RecordDataHelper.GetRightList(groupID));
        }


        /// <summary>
        /// 新增分组并持久化
        /// </summary>
        /// <param name="groupName">分组名称，为空时不新增</param>
        /// <returns>操作结果，"1" 表示成功，"0" 表示名称为空</returns>
        public ActionResult AddGroup(string groupName)
        {
            var result = "0";
            if (!string.IsNullOrWhiteSpace(groupName))
            {
                RecordDataHelper.AddGroup(groupName);

                result = "1";
            }
            return Content(result);
        }

        /// <summary>
        /// 输出用于分组下拉框的 option 列表 HTML
        /// </summary>
        /// <returns>option 标签拼接结果，无分组时为空字符串</returns>
        public ActionResult GetGroups()
        {
            return Content(RecordDataHelper.GetGroups());
        }

        /// <summary>
        /// 向指定分组插入接口条目并持久化，标题、地址与请求体在入库前先做 URL 解码
        /// </summary>
        /// <param name="groupID">目标分组编号</param>
        /// <param name="title">接口标题，入库前做 URL 解码</param>
        /// <param name="method">请求方法，如 GET、POST</param>
        /// <param name="url">接口地址，入库前做 URL 解码</param>
        /// <param name="json">请求体 JSON，非空时做 URL 解码</param>
        /// <returns>操作结果，"1" 表示成功，"0" 表示必填参数缺失</returns>
        public ActionResult AddItem(string groupID, string title, string method, string url, string json)
        {
            var result = "0";
            if (!string.IsNullOrWhiteSpace(groupID) && !string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(url))
            {
                title = SAEA.Http.HttpUtility.UrlDecode(title);

                url = SAEA.Http.HttpUtility.UrlDecode(url);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    json = SAEA.Http.HttpUtility.UrlDecode(json);
                }

                RecordDataHelper.AddItem(groupID, title, method, url, json);

                result = "1";
            }
            return Content(result);
        }

        /// <summary>
        /// 修改分组名称并持久化，名称入库前做 URL 解码
        /// </summary>
        /// <param name="groupID">待修改的分组编号</param>
        /// <param name="groupName">新的分组名称，入库前做 URL 解码</param>
        /// <returns>操作结果，"1" 表示成功，"0" 表示参数缺失</returns>
        public ActionResult UpdateGroup(string groupID, string groupName)
        {
            var result = "0";
            if (!string.IsNullOrWhiteSpace(groupID) && !string.IsNullOrWhiteSpace(groupName))
            {
                groupName = SAEA.Http.HttpUtility.UrlDecode(groupName);

                RecordDataHelper.UpdateGroup(groupID, groupName);

                result = "1";
            }
            return Content(result);
        }


        /// <summary>
        /// 删除指定分组及其全部接口条目并持久化
        /// </summary>
        /// <param name="groupID">待删除的分组编号</param>
        /// <returns>操作结果，"1" 表示成功，"0" 表示分组编号为空</returns>
        public ActionResult RemoveGroup(string groupID)
        {
            var result = "0";
            if (!string.IsNullOrWhiteSpace(groupID))
            {
                RecordDataHelper.RemoveGroup(groupID);

                result = "1";
            }
            return Content(result);
        }

        /// <summary>
        /// 删除分组下的指定接口条目并持久化
        /// </summary>
        /// <param name="groupID">条目所属分组编号</param>
        /// <param name="itemID">待删除的条目编号</param>
        /// <returns>操作结果，"1" 表示成功，"0" 表示参数缺失</returns>
        public ActionResult RemoveItem(string groupID, string itemID)
        {
            var result = "0";
            if (!string.IsNullOrWhiteSpace(groupID) && !string.IsNullOrWhiteSpace(itemID))
            {
                RecordDataHelper.RemoveItem(groupID, itemID);

                result = "1";
            }
            return Content(result);
        }


        /// <summary>
        /// 获取单个接口条目的 JSON，参数缺失或查无此项时返回空内容
        /// </summary>
        /// <param name="groupID">条目所属分组编号</param>
        /// <param name="itemID">条目编号</param>
        /// <returns>条目 JSON，未找到时为空字符串</returns>
        public ActionResult GetItem(string groupID, string itemID)
        {
            ListItem listItem = new ListItem();

            if (!string.IsNullOrWhiteSpace(groupID) && !string.IsNullOrWhiteSpace(itemID))
            {
                listItem = RecordDataHelper.GetItem(groupID, itemID);
            }

            if (listItem == null)
                return Content(string.Empty);
            return Json(listItem);
        }


        /// <summary>
        /// 按标题或地址关键字检索接口条目，最多返回 10 条 HTML 片段
        /// </summary>
        /// <param name="keywords">搜索关键字，入库前做 URL 解码；为空时返回前 10 条</param>
        /// <returns>匹配条目 HTML，无匹配时为「找不到任何内容」</returns>
        public ActionResult Search(string keywords)
        {
            return Content(RecordDataHelper.Search(keywords));
        }



        /// <summary>
        /// 导入整份接口数据，URL 解码后反序列化并覆盖写入数据文件，异常一律按失败返回
        /// </summary>
        /// <param name="json">整份数据的 JSON 文本，导入前做 URL 解码</param>
        /// <returns>操作结果，"1" 表示导入成功，"0" 表示内容为空或解析失败</returns>
        public ActionResult Import(string json)
        {
            var result = "0";

            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                {
                    json = SAEA.Http.HttpUtility.UrlDecode(json);

                    var rd = Deserialize<RecordData>(json);

                    if (rd != null)
                    {
                        RecordDataHelper.Write(rd);

                        result = "1";
                    }
                }
            }
            catch { }

            return Content(result);
        }

        /// <summary>
        /// 导出当前数据文件中的全部接口数据
        /// </summary>
        /// <returns>数据文件反序列化结果的 JSON，读取失败时为 null</returns>
        public ActionResult Export()
        {
            return Json(RecordDataHelper.Read());
        }

        /// <summary>
        /// 转发一次 HTTP 请求并按响应内容返回，method 为 POST 时提交 data，否则发起 GET
        /// </summary>
        /// <param name="method">请求方法，取 POST 时走 POST，其余值走 GET</param>
        /// <param name="url">目标地址，请求前做 URL 解码</param>
        /// <param name="data">POST 请求体，请求前做 URL 解码</param>
        /// <returns>目标站点的响应文本，请求异常时为异常消息</returns>
        public ActionResult Request(string method, string url, string data)
        {
            var result = string.Empty;

            if (!string.IsNullOrWhiteSpace(method) && !string.IsNullOrWhiteSpace(url))
            {
                url = SAEA.Http.HttpUtility.UrlDecode(url);
            }

            if (!string.IsNullOrWhiteSpace(data))
            {
                data = SAEA.Http.HttpUtility.UrlDecode(data);
            }

            if (method == "POST")
            {
                result = WebClientHelper.Post(url, data);
            }
            else
            {
                result = WebClientHelper.Get(url);
            }
            return Content(result);
        }
    }
}
