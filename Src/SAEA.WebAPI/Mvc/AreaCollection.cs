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
using SAEA.Common;
using SAEA.WebAPI.Common;
using SAEA.WebAPI.Http;
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
                    MethodBase mb = ss.GetFrames().Last().GetMethod();
                    var space = mb.DeclaringType.Namespace;
                    var tt = mb.DeclaringType.Assembly.GetTypes().Where(b => b.FullName.Contains(ConstHelper.CONTROLLERSPACE)).ToList();
                    if (tt == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合MVC规范！");
                    _list.AddRange(tt);
                }
            }
        }
        /// <summary>
        /// 加载用户自定义分离的controller
        /// </summary>
        /// <param name="controllerNameSpace"></param>
        public static void RegistAll(string controllerNameSpace)
        {
            lock (_locker)
            {
                var fileName = controllerNameSpace + ".dll";
                var assembly = Assembly.LoadFile(PathHelper.GetFullName(fileName));
                var tt = assembly.GetTypes().Where(b => b.FullName.Contains(ConstHelper.CONTROLLERSPACE)).ToList();
                if (tt == null) throw new Exception("当前项目中找不到Controllers空间或命名不符合MVC规范！");
                _list.AddRange(tt);
            }
        }

        static NameValueItem _controllerActionName = null;

        /// <summary>
        /// 设置一个默认值
        /// </summary>
        /// <param name="controllerName"></param>
        /// <param name="actionName"></param>
        public static void SetDefault(string controllerName, string actionName)
        {
            _controllerActionName = new NameValueItem
            {
                Name = controllerName.ToLower(),
                Value = actionName.ToLower()
            };
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
                    if (_controllerActionName != null)
                    {
                        var d = _list.Where(b => string.Compare(b.Name, _controllerActionName.Name, true) == 0 || string.Compare(b.Name, _controllerActionName.Name + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                        if (d != null)
                        {
                            return Invoke(httpContext, d, _controllerActionName.Value, nameValues, isPost);
                        }
                    }
                }
                else if (arr.Length >= 2)
                {
                    var controllerName = arr[arr.Length - 2];

                    var first = _list.Where(b => string.Compare(b.Name, controllerName + ConstHelper.CONTROLLERNAME, true) == 0).FirstOrDefault();

                    if (first != null)
                    {
                        return Invoke(httpContext, first, arr[arr.Length - 1], nameValues, isPost);
                    }
                    else
                    {
                        var filePath = httpContext.Server.MapPath(httpContext.Request.Url);
                        if (File.Exists(filePath))
                        {
                            return new FileResult(filePath);
                        }
                    }

                }

                return new ContentResult("o_o，找不到任何内容", System.Net.HttpStatusCode.NotFound);
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
                    var routing = RouteTable.GetOrAdd(controller, controller.Name, actionName, isPost);

                    if (routing == null)
                    {
                        throw new Exception($"o_o，找不到：{controller.Name}/{actionName} 当前请求为:{(isPost ? ConstHelper.HTTPPOST : ConstHelper.HTTPGET)}");
                    }

                    routing.Instance.HttpContext = httpContext;

                    var nargs = new object[] { httpContext };

                    if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
                    {
                        foreach (var arr in routing.FilterAtrrs)
                        {
                            var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING)).Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new ContentResult("o_o，当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                            }
                        }
                    }

                    if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
                    {
                        foreach (var arr in routing.ActionFilterAtrrs)
                        {
                            var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTING)).Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new ContentResult("o_o，当前逻辑已被拦截！", System.Net.HttpStatusCode.NotAcceptable);
                            }
                        }
                    }

                    var result = MethodInvoke(routing.Action, routing.ActionInvoker, routing.Instance, nameValues);

                    nargs = new object[] { httpContext, result };

                    if (routing.FilterAtrrs != null && routing.FilterAtrrs.Count > 0)
                    {
                        foreach (var arr in routing.FilterAtrrs)
                        {
                            FastInvoke.GetMethodInvoker(arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED)).Invoke(arr, nargs);
                        }
                    }

                    if (routing.ActionFilterAtrrs != null && routing.ActionFilterAtrrs.Count > 0)
                    {
                        foreach (var arr in routing.FilterAtrrs)
                        {
                            FastInvoke.GetMethodInvoker(arr.GetType().GetMethod(ConstHelper.ONACTIONEXECUTED)).Invoke(arr, nargs);
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("o_o，找不到"))
                    {
                        return new ContentResult(ex.Message, System.Net.HttpStatusCode.NotFound);
                    }
                    else
                    {
                        return new ContentResult("→_→，出错了：" + ex.Message, System.Net.HttpStatusCode.InternalServerError);
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
        private static ActionResult MethodInvoke(MethodInfo action, FastInvoke.FastInvokeHandler actionInvoker, object obj, NameValueCollection nameValues)
        {
            ActionResult result = null;
            try
            {
                var @params = action.GetParameters();

                if (@params != null && @params.Length > 0)
                {
                    var list = FillPamars(@params, nameValues);

                    result = (ActionResult)actionInvoker.Invoke(obj, list.ToArray());
                }
                else
                {
                    result = (ActionResult)actionInvoker.Invoke(obj, null);
                }
            }
            catch (Exception ex)
            {
                result = new ContentResult($"→_→，出错了：{obj}/{action.Name},出现异常：{ex.Message}", System.Net.HttpStatusCode.InternalServerError);
            }
            return result;
        }


        /// <summary>
        /// 参数填充
        /// </summary>
        /// <param name="params"></param>
        /// <param name="nameValues"></param>
        /// <returns></returns>
        private static List<object> FillPamars(ParameterInfo[] @params, NameValueCollection nameValues)
        {
            List<object> list = new List<object>();

            foreach (var parma in @params)
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
                        if (string.IsNullOrEmpty(val)) val = "false";

                        if (int.TryParse(val, out int iv))
                        {
                            if (iv == 0) val = "false";
                            else val = "true";
                        }

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
                    else
                    {
                        throw new Exception($"参数{parma.Name}值{val}不正确！");
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
                    else
                    {
                        var modelType = parma.ParameterType;

                        var model = Activator.CreateInstance(modelType);

                        var properties = modelType.GetProperties();

                        if (properties != null)
                        {
                            foreach (var property in properties)
                            {
                                var item = nameValues.Get(property.Name);

                                if (item != null)
                                {
                                    val = item.Value;

                                    if (property.PropertyType == typeof(System.Int32))
                                    {
                                        if (int.TryParse(val, out int v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Int64))
                                    {
                                        if (long.TryParse(val, out long v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Single))
                                    {
                                        if (float.TryParse(val, out float v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Double))
                                    {
                                        if (double.TryParse(val, out double v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.DateTime))
                                    {
                                        if (DateTime.TryParse(val, out DateTime v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Boolean))
                                    {
                                        if (string.IsNullOrEmpty(val)) val = "false";

                                        if (int.TryParse(val, out int iv))
                                        {
                                            if (iv == 0) val = "false";
                                            else val = "true";
                                        }

                                        if (bool.TryParse(val, out bool v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.Byte))
                                    {
                                        if (byte.TryParse(val, out byte v))
                                        {
                                            property.SetValue(model, v);
                                        }
                                        else throw new Exception($"参数{property.Name}值{val}不正确！");
                                    }
                                    else if (property.PropertyType == typeof(System.String))
                                    {
                                        property.SetValue(model, val);
                                    }
                                }
                            }

                            list.Add(model);
                        }
                        else
                        {
                            list.Add(null);
                        }
                    }
                }
            }
            if (list.Count == 0) return null;
            return list;
        }


    }
}
