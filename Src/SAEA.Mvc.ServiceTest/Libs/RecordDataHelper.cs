/****************************************************************************
*项目名称：SAEA.RESTED.Controllers
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.RESTED.Libs
*类 名 称：RecordDataHelper
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
using SAEA.Common;
using SAEA.RESTED.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAEA.RESTED.Libs
{
    public class RecordDataHelper
    {
        static string temple = $"<div class=\"panel panel-default\"> <div class=\"panel-heading\" role=\"tab\" id=\"@groupID\"><h4 class=\"panel-title\"><a class=\"group-title-lable@collapsed\" role=\"button\" data-toggle=\"collapse\" data-parent=\"#accordion\" href=\"#c@groupID\" aria-expanded=\"@expanded\" aria-controls=\"c@groupID\" data-id=\"@groupID\">@groupName</a><a class=\"text-right glyphicon glyphicon-cog group-edit\"></a><a class=\"text-right glyphicon glyphicon-trash group-delete\" data-toggle=\"modal\" data-target=\"#deleteModal\" data-id=\"@groupID\"></a></h4><div class=\"input-group group-edit-div\"><input type=\"text\" class=\"form-control\" value=\"@groupName\" /><span class=\"input-group-btn\"> <button class=\"btn btn-default edit-group-btn\" type=\"button\" data-id=\"@groupID\"><span class=\"glyphicon glyphicon-ok\"></span>  </button></span></div>  </div>  <div id=\"c@groupID\" class=\"panel-collapse collapse@in\" role=\"tabpanel\" aria-labelledby=\"@groupID\"><div class=\"panel-body\">@itemList</div>  </div></div>";

        static string itemList = "<div class=\"list-item\"><a class=\"urlLink\" data-group=\"@groupID\" data-id=\"@itemID\" title=\"@itemUrl\"><p class=\"list-item-title\">@itemTitle</p><p>@itemUrl</p></a> <span class=\"text-right glyphicon glyphicon-trash delteItem\" data-toggle=\"modal\" data-target=\"#deleteItemModal\" data-group=\"@groupID\" data-id=\"@itemID\"></span></div>";


        static string searchItem = "<div data-group=\"@groupID\" data-id=\"@itemID\"><p class=\"search-item-title\">@itemTitle</p><p>@itemUrl</p></div>";


        static string path = Path.Combine(Directory.GetCurrentDirectory(), "SAEA.RESTED.json");

        static RecordData RecordData;


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

        public static void Write(RecordData data)
        {
            try
            {
                var json = SerializeHelper.Serialize(data);

                FileHelper.WriteString(path, json);
            }
            catch { }
        }

        public static string GetID()
        {
            return Guid.NewGuid().ToString("N");
        }


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

        public static void AddGroup(string groupName)
        {
            if (RecordData == null)
            {
                RecordData = new RecordData();
            }
            RecordData.Groups.Add(new GroupData() { GroupID = GetID(), GroupName = groupName });

            Write(RecordData);
        }


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
                    keywords = SAEA.Http.Base.HttpUtility.UrlDecode(keywords);

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
