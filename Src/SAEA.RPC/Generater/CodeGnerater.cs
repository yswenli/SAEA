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
*命名空间：SAEA.RPC.Generater
*文件名： CodeGnerater
*版本号： v26.4.23.1
*唯一标识：55e3d312-785e-4ff8-803c-f5a5d3806481
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/05/25 17:28:26
*描述：CodeGnerater类
*
*=====================================================================
*修改标记
*修改时间：2026/11/11 10:47:08
*修改人： yswenli
*版本号： v26.11.5.1
*描述：生成物补齐 XML 注释、为每个服务方法生成 Async 别名并清理跨次生成缓存
*
*****************************************************************************/
using SAEA.Common;
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SAEA.RPC.Generater
{
    /// <summary>
    /// 代码生成器,
    /// 调用此代码的位置需放在生产者所在项目，否则会出现无法注册问题
    /// </summary>
    public static class CodeGnerater
    {
        static string space4 = "    ";

        /// <summary>
        /// 获取指定数量的空格
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        static string GetSpace(int num = 1)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < num; i++)
            {
                sb.Append(space4);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 获取变量名
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        static string GetSuffixStr(string str)
        {
            return "_" + StringHelper.Substring(str, 0, 2);
        }

        /// <summary>
        /// 生成代码头部
        /// </summary>
        /// <returns></returns>
        static string Header(params string[] usings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("/*******");
            sb.AppendLine("* 此代码为SAEA.RPC.Generater生成");
            sb.AppendLine("* SAEA.RPC.Generater Developed by yswenli");
            sb.AppendLine($"* 尽量不要修改此代码 {DateTimeHelper.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
            sb.AppendLine("*******/" + Environment.NewLine);
            sb.AppendLine("using System;");
            if (usings != null)
            {
                foreach (var u in usings)
                {
                    sb.AppendLine(u);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 追加 XML 摘要注释
        /// </summary>
        /// <param name="sb">目标构建器</param>
        /// <param name="indent">缩进层级</param>
        /// <param name="summary">摘要文本</param>
        static void AppendSummary(StringBuilder sb, int indent, string summary)
        {
            var space = GetSpace(indent);

            sb.AppendLine(space + "/// <summary>");

            foreach (var line in summary.Split('\n'))
            {
                sb.AppendLine(space + "/// " + line.TrimEnd('\r'));
            }

            sb.AppendLine(space + "/// </summary>");
        }

        /// <summary>
        /// 追加 XML 参数注释
        /// </summary>
        /// <param name="sb">目标构建器</param>
        /// <param name="indent">缩进层级</param>
        /// <param name="name">参数名</param>
        /// <param name="desc">参数说明</param>
        static void AppendParam(StringBuilder sb, int indent, string name, string desc)
        {
            sb.AppendLine(GetSpace(indent) + $"/// <param name=\"{name}\">{desc}</param>");
        }

        /// <summary>
        /// 追加 XML 返回值注释
        /// </summary>
        /// <param name="sb">目标构建器</param>
        /// <param name="indent">缩进层级</param>
        /// <param name="desc">返回值说明</param>
        static void AppendReturns(StringBuilder sb, int indent, string desc)
        {
            sb.AppendLine(GetSpace(indent) + $"/// <returns>{desc}</returns>");
        }

        /// <summary>
        /// 追加服务方法及其同步实现
        /// </summary>
        /// <param name="sb">目标构建器</param>
        /// <param name="serviceName">服务名</param>
        /// <param name="methodName">本地方法名</param>
        /// <param name="remoteMethodName">服务端方法名，Async 别名沿用原名</param>
        /// <param name="returnTypeName">返回类型名</param>
        /// <param name="argsStr">参数声明</param>
        /// <param name="argsInput">实参列表</param>
        static void AppendServiceMethod(StringBuilder sb, string serviceName, string methodName, string remoteMethodName, string returnTypeName, string argsStr, string argsInput)
        {
            sb.AppendLine(GetSpace(2) + $"public {returnTypeName} {methodName}({argsStr})");
            sb.AppendLine(GetSpace(2) + "{");
            sb.AppendLine(GetSpace(3) + $"return _serviceConsumer.RemoteCall<{returnTypeName}>(\"{serviceName}\", \"{remoteMethodName}\"{argsInput});");
            sb.AppendLine(GetSpace(2) + "}");
        }

        static string _proxyStr;

        static List<string> _serviceStrs = new List<string>();

        static Dictionary<string, string> _modelStrs = new Dictionary<string, string>();

        /// <summary>
        /// 生成代理代码
        /// </summary>
        /// <param name="spaceName"></param>
        internal static void GenerateProxy(string spaceName)
        {
            StringBuilder csStr = new StringBuilder();


            var header = Header("using System.Collections.Generic;", "using SAEA.Common;", "using SAEA.RPC.Consumer;", "using SAEA.RPC.Model;", "using SAEA.Sockets.Pipeline;", $"using {spaceName}.Consumer.Model;", $"using {spaceName}.Consumer.Service;");
            csStr.AppendLine(header);

            csStr.AppendLine($"namespace {spaceName}.Consumer");
            csStr.AppendLine("{");


            AppendSummary(csStr, 1, "RPC服务代理类");
            csStr.AppendLine($"{GetSpace(1)}public class RPCServiceProxy");
            csStr.AppendLine(GetSpace(1) + "{");


            AppendSummary(csStr, 2, "处理器链装配回调：每条连接在 RPC 解码之后、异常收口之前调用一次，\n使用者在此追加自己的入站处理器，可观察应答、推送通知与链上异常。\n代理在构造期建立连接，请在构造函数中注入，构造完成后再赋值会抛 InvalidOperationException");
            csStr.AppendLine(GetSpace(2) + "public Action<string, IChannelPipeline> ConfigurePipeline");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "get { return _serviceConsumer.ConfigurePipeline; }");
            csStr.AppendLine(GetSpace(3) + "set { _serviceConsumer.ConfigurePipeline = value; }");
            csStr.AppendLine(GetSpace(2) + "}\r\n");
            csStr.AppendLine(GetSpace(2) + "ServiceConsumer _serviceConsumer;\r\n");


            AppendSummary(csStr, 2, "获取连接状态");
            csStr.AppendLine(GetSpace(2) + "public bool IsConnected");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "get { return _serviceConsumer.IsConnected; }");
            csStr.AppendLine(GetSpace(2) + "}");


            csStr.AppendLine();
            AppendSummary(csStr, 2, "构造函数");
            AppendParam(csStr, 2, "uri", "服务地址");
            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(string uri = \"rpc://127.0.0.1:39654\") : this(uri, 4, 5, 10 * 1000) { }");

            csStr.AppendLine();
            AppendSummary(csStr, 2, "构造函数");
            AppendParam(csStr, 2, "uri", "服务地址");
            AppendParam(csStr, 2, "links", "连接数");
            AppendParam(csStr, 2, "retry", "重试次数");
            AppendParam(csStr, 2, "timeOut", "超时时间");
            AppendParam(csStr, 2, "configurePipeline", "处理器链装配回调，在连接建立前写下，建链时生效");
            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(string uri, int links = 4, int retry = 5, int timeOut = 10 * 1000, Action<string, IChannelPipeline> configurePipeline = null)");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer = new ServiceConsumer(new Uri(uri), links, retry, timeOut, configurePipeline);");

            var names = RPCMapping.GetServiceNames();

            if (names != null)
            {
                foreach (var name in names)
                {
                    csStr.AppendLine(GetSpace(3) + GetSuffixStr(name) + $" = new {name}(_serviceConsumer);");
                }
            }
            csStr.AppendLine(GetSpace(2) + "}");

            csStr.AppendLine();
            AppendSummary(csStr, 2, "重连");
            AppendReturns(csStr, 2, "重连成功返回 true");
            csStr.AppendLine(GetSpace(2) + "public bool Reconnect()");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "return _serviceConsumer.Reconnect();");
            csStr.AppendLine(GetSpace(2) + "}");
            csStr.AppendLine();

            if (names != null)
            {
                foreach (var name in names)
                {
                    var suffixStr = GetSuffixStr(name);

                    csStr.AppendLine(GetSpace(2) + $"{name} {suffixStr};");
                    csStr.AppendLine();
                    AppendSummary(csStr, 2, $"{name} 服务客户端代理");
                    csStr.AppendLine(GetSpace(2) + $"public {name} {name}");
                    csStr.AppendLine(GetSpace(2) + "{");
                    csStr.AppendLine($"{GetSpace(3)} get{{ return {suffixStr}; }}");
                    csStr.AppendLine(GetSpace(2) + "}");
                    csStr.AppendLine();

                    var list = RPCMapping.GetAll(name);
                    if (list != null)
                    {
                        GenerateService(spaceName, name, list);
                    }
                }
            }


            AppendSummary(csStr, 2, "注册接收通知；注册后服务端推送会以 <see cref=\"SAEA.RPC.Net.RSocketMsg\"/> 形式进入处理器链");
            csStr.AppendLine(GetSpace(2) + "public void RegistReceiveNotice()");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer.RegistReceiveNotice();");
            csStr.AppendLine(GetSpace(2) + "}");


            csStr.AppendLine();
            AppendSummary(csStr, 2, "释放资源");
            csStr.AppendLine(GetSpace(2) + "public void Dispose()");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer.Dispose();");
            csStr.AppendLine(GetSpace(2) + "}");

            csStr.AppendLine(GetSpace(1) + "}");
            csStr.AppendLine("}");
            _proxyStr = csStr.ToString();
        }
        /// <summary>
        /// 生成调用服务代码
        /// </summary>
        /// <param name="spaceName"></param>
        /// <param name="serviceName"></param>
        /// <param name="methods"></param>
        internal static void GenerateService(string spaceName, string serviceName, Dictionary<string, ServiceInfo> methods)
        {
            StringBuilder csStr = new StringBuilder();
            csStr.AppendLine($"namespace {spaceName}.Consumer.Service");
            csStr.AppendLine("{");
            AppendSummary(csStr, 1, $"{serviceName} 的客户端代理");
            csStr.AppendLine($"{GetSpace(1)}public class {serviceName}");
            csStr.AppendLine(GetSpace(1) + "{");
            csStr.AppendLine(GetSpace(2) + "ServiceConsumer _serviceConsumer;");
            csStr.AppendLine();
            AppendSummary(csStr, 2, "构造函数");
            AppendParam(csStr, 2, "serviceConsumer", "服务消费者");
            csStr.AppendLine(GetSpace(2) + $"public {serviceName}(ServiceConsumer serviceConsumer)");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer = serviceConsumer;");
            csStr.AppendLine(GetSpace(2) + "}");

            foreach (var item in methods)
            {
                var rtype = item.Value.Method.ReturnType;

                if (rtype != null && (rtype.IsClass || rtype.IsEnum))
                {
                    if (rtype.IsGenericType)
                    {
                        if (TypeHelper.ListTypeStrs.Contains(rtype.Name))
                        {
                            var t = rtype.GetGenericArguments()[0];

                            if (!t.IsAbstract && t.IsClass)
                            {
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t.Name}"))
                                {
                                    GenerateModel(spaceName, t);
                                }
                            }

                        }
                        else if (TypeHelper.DicTypeStrs.Contains(rtype.Name))
                        {
                            var t1 = rtype.GetGenericArguments()[0];
                            var t2 = rtype.GetGenericArguments()[1];

                            if (!t1.IsAbstract && !t1.IsArray && t1.IsClass)
                            {
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t1.Name}"))
                                {
                                    GenerateModel(spaceName, t1);
                                }
                            }
                            if (!t2.IsAbstract && !t2.IsArray && t2.IsClass)
                            {
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t2.Name}"))
                                {
                                    GenerateModel(spaceName, t2);
                                }
                            }
                        }
                        else
                        {
                            var t = rtype.GetGenericTypeDefinition();
                            var p = rtype.GetGenericArguments()[0];
                            if (!t.IsAbstract && !t.IsArray && t.IsClass)
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t.Name}"))
                                {
                                    GenerateModel(spaceName, t);
                                }
                            if (!p.IsAbstract && !p.IsArray && p.IsClass)
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{p.Name}"))
                                {
                                    GenerateModel(spaceName, t);
                                }
                        }
                    }
                    else
                    {
                        if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{rtype.Name}"))
                        {
                            GenerateModel(spaceName, rtype);
                        }
                    }
                }

                var argsStr = new StringBuilder();

                var argsInput = new StringBuilder();

                if (item.Value.Pamars != null)
                {
                    int i = 0;
                    foreach (var arg in item.Value.Pamars)
                    {
                        i++;

                        if (arg.Value.IsGenericType)
                        {
                            argsStr.Append(TypeHelper.GetTypeName(arg.Value));
                            argsStr.Append(" ");
                            argsStr.Append(arg.Key);
                        }
                        else
                        {
                            argsStr.Append(arg.Value.Name);
                            argsStr.Append(" ");
                            argsStr.Append(arg.Key);
                        }


                        if (i < item.Value.Pamars.Count)
                            argsStr.Append(", ");

                        if (arg.Value != null && (arg.Value.IsClass || arg.Value.IsEnum))
                        {
                            if (arg.Value.IsGenericType)
                            {
                                if (TypeHelper.ListTypeStrs.Contains(arg.Value.Name))
                                {
                                    var t = arg.Value.GetGenericArguments()[0];

                                    if (!t.IsAbstract && !t.IsArray && !t.IsGenericType && t.IsClass)
                                    {
                                        if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t.Name}"))
                                        {
                                            GenerateModel(spaceName, t);
                                        }
                                    }
                                }
                                else if (TypeHelper.DicTypeStrs.Contains(arg.Value.Name))
                                {
                                    var t1 = arg.Value.GetGenericArguments()[0];
                                    var t2 = arg.Value.GetGenericArguments()[1];

                                    if (!t1.IsAbstract && !t1.IsArray && !t1.IsGenericType && t1.IsClass)
                                    {
                                        if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t1.Name}"))
                                        {
                                            GenerateModel(spaceName, t1);
                                        }
                                    }
                                    if (!t2.IsAbstract && !t2.IsArray && !t2.IsGenericType && t2.IsClass)
                                    {
                                        if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t2.Name}"))
                                        {
                                            GenerateModel(spaceName, t2);
                                        }
                                    }
                                }
                                else
                                {
                                    if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{arg.Value.Name}"))
                                    {
                                        GenerateModel(spaceName, arg.Value);
                                    }

                                    var gs = arg.Value.GetGenericArguments();

                                    foreach (var t in gs)
                                    {
                                        if (!t.IsAbstract && !t.IsArray && t.IsClass)
                                        {
                                            if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{t.Name}"))
                                            {
                                                GenerateModel(spaceName, t);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{arg.Value.Name}"))
                                {
                                    GenerateModel(spaceName, arg.Value);
                                }
                            }
                        }

                        argsInput.Append(", ");
                        argsInput.Append(arg.Key);
                    }
                }
                var returnTypeName = rtype.IsGenericType ? TypeHelper.GetTypeName(rtype) : rtype.Name;

                csStr.AppendLine();
                AppendSummary(csStr, 2, $"调用服务端 {item.Key} 方法");
                if (item.Value.Pamars != null)
                {
                    foreach (var arg in item.Value.Pamars)
                    {
                        AppendParam(csStr, 2, arg.Key, "参数 " + arg.Key);
                    }
                }
                AppendReturns(csStr, 2, "远程调用结果");
                AppendServiceMethod(csStr, serviceName, item.Key, item.Key, returnTypeName, argsStr.ToString(), argsInput.ToString());

                // 为每个方法补一个同名的 Async 别名，便于直接以 async 方式调用；实现仍是同步远程调用，服务端方法名不变。
                if (!item.Key.EndsWith("Async", StringComparison.Ordinal))
                {
                    csStr.AppendLine();
                    AppendSummary(csStr, 2, $"异步调用服务端 {item.Key} 方法；实现为同步远程调用，并非真正的异步");
                    if (item.Value.Pamars != null)
                    {
                        foreach (var arg in item.Value.Pamars)
                        {
                            AppendParam(csStr, 2, arg.Key, "参数 " + arg.Key);
                        }
                    }
                    AppendReturns(csStr, 2, "远程调用结果");
                    AppendServiceMethod(csStr, serviceName, item.Key + "Async", item.Key, returnTypeName, argsStr.ToString(), argsInput.ToString());
                }
            }

            csStr.AppendLine(GetSpace(1) + "}");
            csStr.AppendLine("}");
            _serviceStrs.Add(csStr.ToString());
        }

        /// <summary>
        /// 生成实体代码
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        /// <summary>
        /// 生成实体代码
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        internal static void GenerateModel(string spaceName, Type type)
        {
            if (type.IsInterface) throw new Exception("在实体及其属性中不支持接口：" + TypeHelper.GetTypeName(type));

            var modelKey = $"{spaceName}.Consumer.Model.{type.Name}";
            if (_modelStrs.ContainsKey(modelKey))
            {
                return;
            }

            if (type.IsArray)
            {
                var stype = type.GetElementType();
                GenerateModel(spaceName, stype);
            }
            else if ((type.IsClass && !type.IsNested && type != typeof(string) && type != typeof(object)))
            {
                if (!TypeHelper.ListTypeStrs.Contains(type.Name) && !TypeHelper.DicTypeStrs.Contains(type.Name))
                {
                    StringBuilder csStr = new StringBuilder();
                    csStr.AppendLine($"namespace {spaceName}.Consumer.Model");
                    csStr.AppendLine("{");
                    AppendSummary(csStr, 1, $"{TypeHelper.GetTypeName(type)} 实体");
                    csStr.AppendLine($"{GetSpace(1)}public class {TypeHelper.GetTypeName(type)}");
                    csStr.AppendLine(GetSpace(1) + "{");
                    var ps = type.GetProperties();
                    foreach (var p in ps)
                    {
                        AppendSummary(csStr, 2, $"属性 {p.Name}");
                        csStr.AppendLine($"{GetSpace(2)}public {TypeHelper.GetTypeName(p.PropertyType)} {p.Name}");
                        csStr.AppendLine(GetSpace(2) + "{");
                        csStr.AppendLine(GetSpace(3) + "get;set;");
                        csStr.AppendLine(GetSpace(2) + "}");
                        csStr.AppendLine();
                    }
                    csStr.AppendLine(GetSpace(1) + "}");
                    csStr.AppendLine("}");
                    if (!_modelStrs.ContainsKey(modelKey))
                    {
                        _modelStrs.Add(modelKey, csStr.ToString());
                    }

                    foreach (var p in ps)
                    {
                        GenerateModel(spaceName, p.PropertyType);

                        if (p.PropertyType.IsGenericType)
                        {
                            var stypes = p.PropertyType.GetGenericArguments();

                            foreach (var item in stypes)
                            {
                                GenerateModel(spaceName, item);
                            }
                        }
                    }
                }
                if (type.IsGenericType)
                {
                    var stypes = type.GetGenericArguments();

                    foreach (var item in stypes)
                    {
                        GenerateModel(spaceName, item);
                    }
                }
            }
            else if (type.IsEnum)
            {
                StringBuilder csStr = new StringBuilder();
                var baseType = Enum.GetUnderlyingType(type);
                csStr.AppendLine($"namespace {spaceName}.Consumer.Model");
                csStr.AppendLine("{");
                AppendSummary(csStr, 1, $"{type.Name} 枚举");
                csStr.AppendLine($"{GetSpace(1)}public enum {type.Name}:{baseType.Name}");
                csStr.AppendLine(GetSpace(1) + "{");
                var values = Enum.GetValues(type);
                foreach (var value in values)
                {
                    AppendSummary(csStr, 2, $"枚举值 {value}");
                    if (values.GetValue(values.Length - 1) == value)
                    {
                        switch (baseType.Name)
                        {
                            case "Byte":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Byte)value}");
                                break;
                            case "Int16":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Int16)value}");
                                break;
                            case "Int64":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Int64)value}");
                                break;
                            default:
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(int)value}");
                                break;
                        }
                    }
                    else
                    {
                        switch (baseType.Name)
                        {
                            case "Byte":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Byte)value},");
                                break;
                            case "Int16":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Int16)value},");
                                break;
                            case "Int64":
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(Int64)value},");
                                break;
                            default:
                                csStr.AppendLine($"{GetSpace(2)}{value.ToString()}={(int)value},");
                                break;
                        }
                    }
                }
                csStr.AppendLine(GetSpace(1) + "}");
                csStr.AppendLine("}");
                if (!_modelStrs.ContainsKey(modelKey))
                {
                    _modelStrs.Add(modelKey, csStr.ToString());
                }
            }
        }



        /// <summary>
        /// 生成客户端C#代码文件
        /// 另外此方法名不宜调整
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="spaceName"></param>
        /// <param name="types"></param>
        public static void Generate(string folder, string spaceName, params Type[] types)
        {
            // 静态缓存跨次生成会累积，先清空，否则同一进程内第二次生成会重复追加服务与实体。
            _serviceStrs.Clear();
            _modelStrs.Clear();
            _proxyStr = null;

            RPCMapping.Regists(types);

            GenerateProxy(spaceName);

            var filePath = Path.Combine(folder, "RPCServiceProxy.cs");

            StringBuilder sb = new StringBuilder();

            sb.AppendLine(_proxyStr);

            if (_serviceStrs != null && _serviceStrs.Count > 0)
            {
                foreach (var serviceStr in _serviceStrs)
                {
                    sb.AppendLine(serviceStr);
                }
            }

            if (_modelStrs != null && _modelStrs.Count > 0)
            {
                foreach (var entry in _modelStrs)
                {
                    sb.AppendLine(entry.Value);
                }
            }

            if (File.Exists(filePath))
                File.Delete(filePath);

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

    }
}