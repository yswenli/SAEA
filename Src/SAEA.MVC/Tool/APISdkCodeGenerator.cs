/****************************************************************************
*项目名称：SAEA.MVC.Tool
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.MVC.Tool
*类 名 称：APISdkCodeGenerator
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/24 15:35:50
*描述：
*=====================================================================
*修改时间：2020/12/24 15:35:50
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
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
            List<Routing> routings = ApiMapping.GetMapping();

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
        static void SaveCsharp(string path, List<Routing> routings)
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
        static void SaveJs(string path, List<Routing> routings)
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
                methodStr = methodStr.Replace("[[Controller]]", routing.ControllerName.Replace("Controller", ""));
                methodStr = methodStr.Replace("[[Action]]", routing.ActionName);
                methodStr = methodStr.Replace("[[Type]]", (routing.IsPost ? "Post" : "Get"));
                sb.AppendLine(methodStr);
            }

            jsStr = jsStr.Replace("[[Method]]", sb.ToString());

            FileHelper.WriteString(fileName, jsStr);
        }
    }
}
