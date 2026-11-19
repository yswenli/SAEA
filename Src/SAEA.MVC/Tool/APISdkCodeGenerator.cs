/****************************************************************************
 * 
  ____    _    _____    _      ____             _        _   
 / ___|  / \  | ____|  / \    / ___|  ___   ___| | _____| |_ 
 \___ \ / _ \ |  _|   / _ \   \___ \ / _ \ / __| |/ / _ \ __|
  ___) / ___ \| |___ / ___ \   ___) | (_) | (__|   <  __/ |_ 
 |____/_/   \_\_____/_/   \_\ |____/ \___/ \___|_|\_\___|\__|
                                                              
 
*Copyright (c) yswenli All Rights Reserved.
*CLR版本： netstandard2.0
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC.Tool
*文件名： APISdkCodeGenerator
*版本号： v26.4.23.1
*唯一标识：f2fe3b27-9abd-4a7d-83cb-3de4af91214f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/25 17:36:47
*描述：APISdkCodeGenerator接口
*
*=====================================================================
*修改标记
*修改时间：2026/11/19 11:16:00
*修改人： yswenli
*版本号： v26.11.16.1
*描述：补 Routing 子命名空间引用，类型 Routing 改为 RouteEntry，地址仍按 RouteEntry.Template 输出
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using SAEA.MVC.Routing;
using SAEA.MVC.Tool.CodeGenerte;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SAEA.MVC.Tool
{
    /// <summary>
    /// SAEA.MVC sdk代码生成工具
    /// </summary>
    public static class APISdkCodeGenerator
    {
        /// <summary>
        /// 保存代码
        /// </summary>
        /// <param name="path"></param>
        /// <param name="codeType"></param>
        public static void Save(string path, CodeType codeType)
        {
            List<RouteEntry> routings = ApiMapping.GetMapping();

            if (codeType == CodeType.Js)
            {
                SaveJs(path, routings);
            }
            else
            {
                SaveCsharp(path, routings);
            }
        }

        /// <summary>
        /// 生成c# sdk代码
        /// </summary>
        /// <param name="path"></param>
        /// <param name="routings"></param>
        static void SaveCsharp(string path, List<RouteEntry> routings)
        {
            var fileName = PathHelper.GetFilePath(path, "SaeaApiSdk.cs");

            var csStr = CSharpTemple.TEMPLE.Replace("[[DateTime]]", DateTimeHelper.Now.ToFString());

            StringBuilder sb = new StringBuilder();

            foreach (var routing in routings)
            {
                var methodStr = "";

                if (routing.ParmaTypes == null || !routing.ParmaTypes.Any())
                {
                    methodStr = CSharpTemple.MethodTemple1;
                }
                else
                {
                    methodStr = CSharpTemple.MethodTemple2;

                    StringBuilder inputs1 = new StringBuilder();
                    StringBuilder inputs2 = new StringBuilder();

                    foreach (var item in routing.ParmaTypes)
                    {
                        if (item.Value.IsClass && !item.Value.IsSealed)
                        {
                            var ppts = item.Value.GetProperties();
                            foreach (var ppt in ppts)
                            {
                                inputs1.Append($"{ppt.PropertyType.Name} {ppt.Name},");
                                if (ppt.PropertyType == typeof(string))
                                {
                                    inputs2.Append($"\"{ppt.Name}=\" + SAEA.Http.HttpUtility.UrlEncode({ppt.Name}) + \"&\" + ");
                                }
                                else
                                {
                                    inputs2.Append($"\"{ppt.Name}=\" + {ppt.Name} + \"&\" + ");
                                }
                            }
                        }
                        else
                        {
                            inputs1.Append($"{item.Key.GetType().Name} {item.Key},");
                            if (item.Key.GetType() == typeof(string))
                            {
                                inputs2.Append($"\"{item.Key}=\" + SAEA.Http.HttpUtility.UrlEncode({item.Key}) + \"&\" + ");
                            }
                            else
                            {
                                inputs2.Append($"\"{item.Key}=\" + {item.Key} + \"&\" + ");
                            }                                
                        }
                    }

                    var inputStr1 = inputs1.ToString(0, inputs1.Length);
                    var inputStr2 = inputs2.ToString(0, inputs2.Length - 9);

                    methodStr = methodStr.Replace("[[Inputs1]]", inputStr1);
                    methodStr = methodStr.Replace("[[Inputs2]]", inputStr2);
                }
                methodStr = methodStr.Replace("[[Path]]", GetPath(routing, false));
                methodStr = methodStr.Replace("[[Controller]]", routing.ControllerName.Replace("Controller", ""));
                methodStr = methodStr.Replace("[[Action]]", routing.ActionName);
                methodStr = methodStr.Replace("[[Type]]", (routing.IsPost ? "Post" : "Get"));
                sb.AppendLine(methodStr);
            }

            csStr = csStr.Replace("[[Method]]", sb.ToString());

            FileHelper.WriteString(fileName, csStr);
        }
        /// <summary>
        /// 生成js sdk代码
        /// </summary>
        /// <param name="path"></param>
        /// <param name="routings"></param>
        static void SaveJs(string path, List<RouteEntry> routings)
        {
            var fileName = PathHelper.GetFilePath(path, "SaeaApiSdk.js");

            var jsStr = JsTemple.TEMPLE.Replace("[[DateTime]]", DateTimeHelper.Now.ToFString());

            StringBuilder sb = new StringBuilder();

            foreach (var routing in routings)
            {
                var methodStr = "";

                if (routing.ParmaTypes == null || !routing.ParmaTypes.Any())
                {
                    methodStr = JsTemple.MethodTemple1;
                }
                else
                {
                    methodStr = JsTemple.MethodTemple2;

                    StringBuilder inputs1 = new StringBuilder();
                    StringBuilder inputs2 = new StringBuilder();

                    foreach (var item in routing.ParmaTypes)
                    {
                        if (item.Value.IsClass && !item.Value.IsSealed)
                        {
                            var ppts = item.Value.GetProperties();
                            foreach (var ppt in ppts)
                            {
                                inputs1.Append(ppt.Name + ",");
                                inputs2.Append(ppt.Name + ":" + ppt.Name + ",");
                            }
                        }
                        else
                        {
                            inputs1.Append(item.Key + ",");
                            inputs2.Append(item.Key + ":" + item.Key + ",");
                        }
                    }

                    var inputStr1 = inputs1.ToString(0, inputs1.Length);
                    var inputStr2 = inputs2.ToString(0, inputs2.Length - 1);

                    methodStr = methodStr.Replace("[[Inputs1]]", inputStr1);
                    methodStr = methodStr.Replace("[[Inputs2]]", inputStr2);
                }
                methodStr = methodStr.Replace("[[Path]]", GetPath(routing, true));
                methodStr = methodStr.Replace("[[Controller]]", routing.ControllerName.Replace("Controller", ""));
                methodStr = methodStr.Replace("[[Action]]", routing.ActionName);
                methodStr = methodStr.Replace("[[Type]]", (routing.IsPost ? "Post" : "Get"));
                sb.AppendLine(methodStr);
            }

            jsStr = jsStr.Replace("[[Method]]", sb.ToString());

            FileHelper.WriteString(fileName, jsStr);
        }

        /// <summary>
        /// 取 SDK 方法中的地址片段：属性路由按模板输出，未声明模板时回落两段式
        /// </summary>
        /// <param name="routing">路由信息</param>
        /// <param name="leadingSlash">是否保留前导 /。JS 模板的 url 不带结尾 /，需保留；C# 模板的 _url 自带结尾 /，需去掉</param>
        /// <returns>地址片段，模板中的 {name} 占位符原样保留</returns>
        static string GetPath(RouteEntry routing, bool leadingSlash)
        {
            if (string.IsNullOrEmpty(routing.Template))
                return (leadingSlash ? "/" : string.Empty) + "api/[[Controller]]/[[Action]]";

            var template = routing.Template;

            if (leadingSlash) return template;

            // 归一化模板自带单个前导 /，而 _url 已以 / 结尾，去掉它才不会被拼成 //
            return template[0] == '/' ? template.Substring(1) : template;
        }
    }
}
