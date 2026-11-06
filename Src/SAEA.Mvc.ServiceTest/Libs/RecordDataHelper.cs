/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Libs
*类 名 称：RecordDataHelper
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
using SAEA.Common.IO;
using SAEA.Common.Serialization;
using SAEA.Http;
using SAEA.RESTED.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.RESTED.Libs
{
    /// <summary>
    /// 接口文档数据的读写与 HTML 渲染助手，数据以 JSON 形式存放在程序目录的 SAEA.RESTED.json
    /// </summary>
    public class RecordDataHelper
    {
        static string temple = $"<div class=\"panel panel-default\"> <div class=\"panel-heading\" role=\"tab\" id=\"@groupID\"><h4 class=\"panel-title\"><a class=\"group-title-lable@collapsed\" role=\"button\" data-toggle=\"collapse\" data-parent=\"#accordion\" href=\"#c@groupID\" aria-expanded=\"@expanded\" aria-controls=\"c@groupID\" data-id=\"@groupID\">@groupName</a><a class=\"text-right glyphicon glyphicon-cog group-edit\"></a><a class=\"text-right glyphicon glyphicon-trash group-delete\" data-toggle=\"modal\" data-target=\"#deleteModal\" data-id=\"@groupID\"></a></h4><div class=\"input-group group-edit-div\"><input type=\"text\" class=\"form-control\" value=\"@groupName\" /><span class=\"input-group-btn\"> <button class=\"btn btn-default edit-group-btn\" type=\"button\" data-id=\"@groupID\"><span class=\"glyphicon glyphicon-ok\"></span>  </button></span></div>  </div>  <div id=\"c@groupID\" class=\"panel-collapse collapse@in\" role=\"tabpanel\" aria-labelledby=\"@groupID\"><div class=\"panel-body\">@itemList</div>  </div></div>";

        static string itemList = "<div class=\"list-item\"><a class=\"urlLink\" data-group=\"@groupID\" data-id=\"@itemID\" title=\"@itemUrl\"><p class=\"list-item-title\">@itemTitle</p><p>@itemUrl</p></a> <span class=\"text-right glyphicon glyphicon-trash delteItem\" data-toggle=\"modal\" data-target=\"#deleteItemModal\" data-group=\"@groupID\" data-id=\"@itemID\"></span></div>";


        static string searchItem = "<div data-group=\"@groupID\" data-id=\"@itemID\"><p class=\"search-item-title\">@itemTitle</p><p>@itemUrl</p></div>";


        static string path = Path.Combine(Directory.GetCurrentDirectory(), "SAEA.RESTED.json");

        static RecordData RecordData;


        /// <summary>
        /// 从数据文件反序列化整份接口数据
        /// </summary>
        /// <returns>数据文件内容，文件缺失或解析失败时为 null</returns>
        public static RecordData Read()
        {
            RecordData result = null;

            try
            {
                var json = FileHelper.ReadString(path);

                result = SerializeHelper.Deserialize<RecordData>(json);
            }
            catch { }

            return result;
        }

        /// <summary>
        /// 将整份接口数据序列化后覆盖写入数据文件，写入异常被忽略
        /// </summary>
        /// <param name="data">待持久化的接口数据</param>
        public static void Write(RecordData data)
        {
            try
            {
                var json = SerializeHelper.Serialize(data);

                FileHelper.WriteString(path, json);
            }
            catch { }
        }

        /// <summary>
        /// 生成不带连字符的 32 位 GUID，用作分组与条目标识
        /// </summary>
        /// <returns>新的分组或条目编号</returns>
        public static string GetID()
        {
            return Guid.NewGuid().ToString("N");
        }


        /// <summary>
        /// 从数据文件重新加载数据并渲染分组折叠面板 HTML，同时刷新静态缓存
        /// </summary>
        /// <param name="groupID">要展开的分组编号，为空时展开第一个分组</param>
        /// <returns>分组面板 HTML，无数据时为空字符串</returns>
        public static string GetRightList(string groupID = "")
        {
            string result = string.Empty;

            RecordData = Read();

            if (RecordData != null && RecordData.Groups != null && RecordData.Groups.Any())
            {
                StringBuilder sb = new StringBuilder();

                var started = false;

                foreach (var item in RecordData.Groups)
                {
                    StringBuilder ssb = new StringBuilder();

                    if (item.List != null && item.List != null && item.List.Any())
                    {
                        foreach (var sitem in item.List)
                        {
                            ssb.Append(itemList.Replace("@groupID", item.GroupID).Replace("@itemID", sitem.ID).Replace("@itemTitle", sitem.Title).Replace("@itemUrl", sitem.Url));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(groupID))
                    {
                        if (!started)
                        {
                            started = true;
                            sb.Append(temple.Replace("@groupID", item.GroupID).Replace("@groupName", item.GroupName).Replace("@collapsed", "").Replace("@expanded", "true").Replace("@in", " in").Replace("@itemList", ssb.ToString()));
                        }
                        else
                        {
                            sb.Append(temple.Replace("@groupID", item.GroupID).Replace("@groupName", item.GroupName).Replace("@collapsed", " collapsed").Replace("@expanded", "false").Replace("@in", "").Replace("@itemList", ssb.ToString()));
                        }
                    }
                    else
                    {
                        if (item.GroupID == groupID)
                        {
                            sb.Append(temple.Replace("@groupID", item.GroupID).Replace("@groupName", item.GroupName).Replace("@collapsed", "").Replace("@expanded", "true").Replace("@in", " in").Replace("@itemList", ssb.ToString()));
                        }
                        else
                        {
                            sb.Append(temple.Replace("@groupID", item.GroupID).Replace("@groupName", item.GroupName).Replace("@collapsed", " collapsed").Replace("@expanded", "false").Replace("@in", "").Replace("@itemList", ssb.ToString()));
                        }
                    }
                }

                result = sb.ToString();
            }

            return result;
        }

        /// <summary>
        /// 新增分组并写回数据文件，只操作静态缓存，不会先加载文件内容
        /// </summary>
        /// <param name="groupName">分组名称</param>
        public static void AddGroup(string groupName)
        {
            if (RecordData == null)
            {
                RecordData = new RecordData();
            }
            RecordData.Groups.Add(new GroupData() { GroupID = GetID(), GroupName = groupName });

            Write(RecordData);
        }


        /// <summary>
        /// 生成分组下拉框的 option 列表，只读静态缓存，不会加载文件
        /// </summary>
        /// <returns>option 标签拼接结果，无分组时为空字符串</returns>
        public static string GetGroups()
        {
            StringBuilder sb = new StringBuilder();

            if (RecordData == null)
            {
                RecordData = new RecordData();
            }

            if (RecordData.Groups != null && RecordData.Groups.Any())
            {
                foreach (var item in RecordData.Groups)
                {
                    sb.AppendLine($"<option value=\"{item.GroupID}\">{item.GroupName}</option>");
                }
            }

            return sb.ToString();
        }


        /// <summary>
        /// 修改指定分组的名称并写回数据文件，分组不存在时不做任何处理
        /// </summary>
        /// <param name="groupID">待修改的分组编号</param>
        /// <param name="groupName">新的分组名称</param>
        public static void UpdateGroup(string groupID, string groupName)
        {
            if (RecordData == null)
            {
                RecordData = new RecordData();
            }

            var group = RecordData.Groups.Where(b => b.GroupID == groupID).FirstOrDefault();

            if (group == null) return;

            group.GroupName = groupName;

            Write(RecordData);
        }


        /// <summary>
        /// 向指定分组头部插入接口条目并写回数据文件
        /// </summary>
        /// <param name="groupID">目标分组编号</param>
        /// <param name="title">接口标题</param>
        /// <param name="method">请求方法</param>
        /// <param name="url">接口地址</param>
        /// <param name="json">请求体 JSON</param>
        public static void AddItem(string groupID, string title, string method, string url, string json)
        {
            if (RecordData == null)
            {
                RecordData = new RecordData();
            }
            else
            {
                var group = RecordData.Groups.Where(b => b.GroupID == groupID).FirstOrDefault();

                if (group != null)
                {
                    group.List.Insert(0, new ListItem()
                    {
                        ID = GetID(),
                        Title = title,
                        Method = method,
                        Url = url,
                        RequestJson = json
                    });
                    Write(RecordData);
                }
            }
        }


        /// <summary>
        /// 删除指定分组及其全部接口条目并写回数据文件，分组不存在时不做处理
        /// </summary>
        /// <param name="groupID">待删除的分组编号</param>
        public static void RemoveGroup(string groupID)
        {
            if (RecordData != null && RecordData.Groups != null && RecordData.Groups.Any())
            {
                var group = RecordData.Groups.Where(b => b.GroupID == groupID).FirstOrDefault();

                if (group != null)
                {
                    RecordData.Groups.Remove(group);

                    Write(RecordData);
                }
            }
        }


        /// <summary>
        /// 删除分组下的指定条目并写回数据文件，分组或条目不存在时不做处理
        /// </summary>
        /// <param name="groupID">条目所属分组编号</param>
        /// <param name="itemID">待删除的条目编号</param>
        public static void RemoveItem(string groupID, string itemID)
        {
            if (RecordData != null && RecordData.Groups != null && RecordData.Groups.Any())
            {
                var group = RecordData.Groups.Where(b => b.GroupID == groupID).FirstOrDefault();

                if (group != null)
                {
                    if (group.List == null || !group.List.Any()) return;

                    var item = group.List.Where(b => b.ID == itemID).FirstOrDefault();

                    if (item == null) return;

                    group.List.Remove(item);

                    Write(RecordData);
                }
            }
        }

        /// <summary>
        /// 读取分组下的指定条目，只查静态缓存，不会加载文件
        /// </summary>
        /// <param name="groupID">条目所属分组编号</param>
        /// <param name="itemID">条目编号</param>
        /// <returns>匹配的条目，分组或条目不存在时为 null</returns>
        public static ListItem GetItem(string groupID, string itemID)
        {
            ListItem listItem = null;

            if (RecordData != null && RecordData.Groups != null && RecordData.Groups.Any())
            {
                var group = RecordData.Groups.Where(b => b.GroupID == groupID).FirstOrDefault();

                if (group != null)
                {
                    if (group.List == null || !group.List.Any()) return listItem;

                    var item = group.List.Where(b => b.ID == itemID).FirstOrDefault();

                    if (item == null) return listItem;

                    listItem = item;
                }
            }

            return listItem;
        }

        /// <summary>
        /// 查询
        /// </summary>
        /// <param name="keywords"></param>
        /// <returns></returns>
        public static string Search(string keywords)
        {
            string result = "找不到任何内容";

            if (RecordData != null && RecordData.Groups != null && RecordData.Groups.Any())
            {
                if (!string.IsNullOrWhiteSpace(keywords))
                    keywords = HttpUtility.UrlDecode(keywords);

                StringBuilder sb = new StringBuilder();

                int max = 10;

                int i = 0;

                foreach (var group in RecordData.Groups)
                {
                    if (group.List != null && group.List.Any())
                    {
                        foreach (var item in group.List)
                        {
                            i++;

                            if (i > max) break;

                            if (string.IsNullOrWhiteSpace(keywords))
                            {
                                sb.Append(searchItem.Replace("@groupID", group.GroupID).Replace("@itemID", item.ID).Replace("@itemTitle", item.Title).Replace("@itemUrl", item.Url));
                            }
                            else
                            {
                                if (item.Title.IndexOf(keywords) > -1 || item.Url.IndexOf(keywords) > -1)
                                {
                                    sb.Append(searchItem.Replace("@groupID", group.GroupID).Replace("@itemID", item.ID).Replace("@itemTitle", item.Title).Replace("@itemUrl", item.Url));
                                }
                                else
                                {
                                    i--;
                                }
                            }
                        }
                    }
                }

                if (sb.Length > 0)
                {
                    result = sb.ToString();
                }

            }
            return result;
        }
    }
}
