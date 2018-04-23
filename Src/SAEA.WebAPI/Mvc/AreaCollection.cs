/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.WebAPI.Mvc
*文件名： AreaCollection
*版本号： V1.0.0.0
*唯一标识：eb956356-8ea4-4657-aec1-458a3654c078
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/10 18:10:16
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/10 18:10:16
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.WebAPI.Http;
using SAEA.WebAPI.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SAEA.WebAPI.Mvc
{
    internal static class AreaCollection
    {
        static object _locker = new object();

        static List<Type> _list = new List<Type>();


        /// <summary>
        /// 记录用户自定义的Controller
        /// </summary>
        public static void RegistAll()
        {
            lock (_locker)
            {
                if (_list.Count < 1)
                {
                    StackTrace ss = new StackTrace(true);
                    MethodBase mb = ss.GetFrame(2).GetMethod();
                    var space = mb.DeclaringType.Namespace;
                    var tt = mb.DeclaringType.Assembly.GetTypes().Where(b => b.FullName.Contains("Controllers")).ToList();
                    if (tt == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合MVC规范！");
                    _list.AddRange(tt);
                }
            }
        }

        /// <summary>
        /// Controller中处理的方法代理
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="url"></param>
        /// <param name="nameValues"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
        public static ActionResult Invoke(HttpContext httpContext, string url, NameValueCollection nameValues, bool isPost)
        {
            lock (_locker)
            {
                var arr = url.Split("/", StringSplitOptions.RemoveEmptyEntries);

                if (arr.Length == 0)
                {
                    var d = _list.Where(b => b.Name.ToLower() == "homecontroller" || b.Name.ToLower() == "indexcontroller").FirstOrDefault();

                    if (d != null)
                    {
                        return Invoke(httpContext, d, "index", nameValues, isPost);
                    }

                }
                else if (arr.Length >= 2)
                {
                    var controllerName = arr[arr.Length - 2];

                    var first = _list.Where(b => b.Name.ToLower() == controllerName.ToLower() + "controller").FirstOrDefault();

                    if (first != null)
                    {
                        return Invoke(httpContext, first, arr[arr.Length - 1], nameValues, isPost);
                    }
                    else
                    {
                        var filePath = httpContext.Server.MapPath(httpContext.Request.URL);
                        if (File.Exists(filePath))
                        {
                            return new FileResult(filePath);
                        }
                    }

                }

                return new ContentResult("NotFound", System.Net.HttpStatusCode.NotFound);
            }
        }

        /// <summary>
        /// MVC处理
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="controller"></param>
        /// <param name="actionName"></param>
        /// <param name="nameValues"></param>
        /// <param name="isPost"></param>
        /// <returns></returns>
        public static ActionResult Invoke(HttpContext httpContext, Type controller, string actionName, NameValueCollection nameValues, bool isPost)
        {
            lock (_locker)
            {
                try
                {
                    var routing = RouteTable.TryGet(controller, controller.Name, actionName, isPost);

                    var property = controller.GetProperty("HttpContext");

                    property.SetValue(routing.Instance, httpContext);

                    if (routing.Atrr != null)
                    {
                        var args = GetVals(nameValues);

                        var nargs = new object[] { httpContext };

                        var goOn = (bool)routing.Atrr.GetType().GetMethod("OnActionExecuting").Invoke(routing.Atrr, nargs.ToArray());

                        if (goOn)
                        {
                            var result = MethodInvoke(routing.Action, routing.Instance, nameValues);
                            nargs = new object[] { httpContext, result };
                            routing.Atrr.GetType().GetMethod("OnActionExecuted").Invoke(routing.Atrr, nargs);
                            return result;
                        }
                        else
                        {
                            return new ContentResult("当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                        }
                    }
                    return MethodInvoke(routing.Action, routing.Instance, nameValues);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("找不到此action"))
                    {
                        return new ContentResult(ex.Message, System.Net.HttpStatusCode.NotFound);
                    }
                    else
                    {
                        return new ContentResult(ex.Message, System.Net.HttpStatusCode.InternalServerError);
                    }
                }
            }
        }

        /// <summary>
        /// 代理执行方法
        /// </summary>
        /// <param name="action"></param>
        /// <param name="obj"></param>
        /// <param name="nameValues"></param>
        /// <returns></returns>
        private static ActionResult MethodInvoke(MethodInfo action, object obj, NameValueCollection nameValues)
        {
            ActionResult result = null;
            try
            {
                List<object> list = new List<object>();

                var parmas = action.GetParameters();

                if (parmas != null && parmas.Length > 0)
                {
                    foreach (var parma in parmas)
                    {
                        string val = string.Empty;

                        if (nameValues == null || nameValues.Count < 1)
                        {
                            throw new Exception($"缺少参数{parma.Name}！");
                        }

                        if (nameValues.TryGetValue(parma.Name, out val))
                        {
                            if (parma.ParameterType == typeof(System.Int32))
                            {
                                if (int.TryParse(val, out int v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.Int64))
                            {
                                if (long.TryParse(val, out long v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.Single))
                            {
                                if (float.TryParse(val, out float v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.Double))
                            {
                                if (double.TryParse(val, out double v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.DateTime))
                            {
                                if (DateTime.TryParse(val, out DateTime v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.Boolean))
                            {
                                if (bool.TryParse(val, out bool v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.Byte))
                            {
                                if (byte.TryParse(val, out byte v))
                                {
                                    list.Add(v);
                                }
                                else throw new Exception($"参数{parma.Name}值{val}不正确！");
                            }
                            else if (parma.ParameterType == typeof(System.String))
                            {
                                list.Add(val);
                            }
                        }
                        else
                        {
                            if (parma.ParameterType == typeof(System.Int32))
                            {
                                list.Add(0);
                            }
                            else if (parma.ParameterType == typeof(System.Int64))
                            {
                                list.Add(0L);
                            }
                            else if (parma.ParameterType == typeof(System.Single))
                            {
                                list.Add(0F);
                            }
                            else if (parma.ParameterType == typeof(System.Double))
                            {
                                list.Add(0D);
                            }
                            else if (parma.ParameterType == typeof(System.DateTime))
                            {
                                list.Add(new DateTime());
                            }
                            else if (parma.ParameterType == typeof(System.Boolean))
                            {
                                list.Add(true);
                            }
                            else if (parma.ParameterType == typeof(System.Byte))
                            {
                                list.Add((byte)0);
                            }
                            else if (parma.ParameterType == typeof(System.String))
                            {
                                list.Add(string.Empty);
                            }
                        }
                    }
                    result = (ActionResult)action.Invoke(obj, list.ToArray());
                }
                else
                {
                    result = (ActionResult)action.Invoke(obj, null);
                }
            }
            catch (Exception ex)
            {
                result = new ContentResult($"{obj}/{action.Name},出现异常：{ex.Message}", System.Net.HttpStatusCode.InternalServerError);
            }
            return result;
        }



        private static object[] GetVals(NameValueCollection nameValues)
        {
            List<object> args = new List<object>();

            if (nameValues != null && nameValues.Count > 0)
            {
                foreach (var val in nameValues.Values)
                {
                    args.Add(val);
                }
            }
            return args.ToArray();
        }


    }
}
