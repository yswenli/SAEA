/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.RPC.Generater
*文件名： CodeGnerater
*版本号： v4.5.1.2
*唯一标识：59ba5e2a-2fd0-444b-a260-ab68c726d7ee
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 18:30:57
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 18:30:57
*修改人： yswenli
*版本号： v4.5.1.2
*描述：
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
            sb.AppendLine($"* 此代码为SAEA.RPC.Generater生成");
            sb.AppendLine($"* 尽量不要修改此代码 {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
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


            var header = Header("using System.Collections.Generic;", "using SAEA.Common;", "using SAEA.RPC.Consumer;", "using SAEA.RPC.Model;", $"using {spaceName}.Consumer.Model;", $"using {spaceName}.Consumer.Service;");
            csStr.AppendLine(header);

            csStr.AppendLine($"namespace {spaceName}.Consumer");
            csStr.AppendLine("{");


            csStr.AppendLine($"{GetSpace(1)}public class RPCServiceProxy");
            csStr.AppendLine(GetSpace(1) + "{");


            csStr.AppendLine(GetSpace(2) + "public event ExceptionCollector.OnErrHander OnErr;\r\n");
            csStr.AppendLine(GetSpace(2) + "public event OnNoticedHandler OnNoticed;\r\n");
            csStr.AppendLine(GetSpace(2) + "ServiceConsumer _serviceConsumer;\r\n");


            csStr.AppendLine(GetSpace(2) + "public bool IsConnected");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "get{ return _serviceConsumer.IsConnected; }");
            csStr.AppendLine(GetSpace(2) + "}");


            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(string uri = \"rpc://127.0.0.1:39654\") : this(uri, 4, 5, 10 * 1000) { }");
            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(string uri, int links = 4, int retry = 5, int timeOut = 10 * 1000)");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "ExceptionCollector.OnErr += ExceptionCollector_OnErr;");

            
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer = new ServiceConsumer(new Uri(uri), links, retry, timeOut);");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer.OnNoticed += _serviceConsumer_OnNoticed;");

            var names = RPCMapping.GetServiceNames();

            if (names != null)
            {
                foreach (var name in names)
                {
                    csStr.AppendLine(GetSpace(3) + GetSuffixStr(name) + $" = new {name}(_serviceConsumer);");
                }
            }
            csStr.AppendLine(GetSpace(2) + "}");

            csStr.AppendLine(GetSpace(2) + "private void ExceptionCollector_OnErr(string name, Exception ex)");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "OnErr?.Invoke(name, ex);");
            csStr.AppendLine(GetSpace(2) + "}");

            csStr.AppendLine(GetSpace(2) + "private void _serviceConsumer_OnNoticed(byte[] serializeData)");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "OnNoticed?.Invoke(serializeData);");
            csStr.AppendLine(GetSpace(2) + "}");

            if (names != null)
            {
                foreach (var name in names)
                {
                    var suffixStr = GetSuffixStr(name);

                    csStr.AppendLine(GetSpace(2) + $"{name} {suffixStr};");
                    csStr.AppendLine(GetSpace(2) + $"public {name} {name}");
                    csStr.AppendLine(GetSpace(2) + "{");
                    csStr.AppendLine($"{GetSpace(3)} get{{ return {suffixStr}; }}");
                    csStr.AppendLine(GetSpace(2) + "}");

                    var list = RPCMapping.GetAll(name);
                    if (list != null)
                    {
                        GenerateService(spaceName, name, list);
                    }
                }
            }


            csStr.AppendLine(GetSpace(2) + "public void RegistReceiveNotice()");
            csStr.AppendLine(GetSpace(2) + "{");
            csStr.AppendLine(GetSpace(3) + "_serviceConsumer.RegistReceiveNotice();");
            csStr.AppendLine(GetSpace(2) + "}");


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
            csStr.AppendLine($"{GetSpace(1)}public class {serviceName}");
            csStr.AppendLine(GetSpace(1) + "{");
            csStr.AppendLine(GetSpace(2) + "ServiceConsumer _serviceConsumer;");
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
                if (rtype.IsGenericType)
                {
                    csStr.AppendLine(GetSpace(2) + $"public {TypeHelper.GetTypeName(rtype)} {item.Key}({argsStr.ToString()})");
                    csStr.AppendLine(GetSpace(2) + "{");
                    csStr.AppendLine(GetSpace(3) + $"return _serviceConsumer.RemoteCall<{TypeHelper.GetTypeName(rtype)}>(\"{serviceName}\", \"{item.Key}\"{argsInput.ToString()});");
                    csStr.AppendLine(GetSpace(2) + "}");

                }
                else
                {
                    csStr.AppendLine(GetSpace(2) + $"public {rtype.Name} {item.Key}({argsStr.ToString()})");
                    csStr.AppendLine(GetSpace(2) + "{");
                    csStr.AppendLine(GetSpace(3) + $"return _serviceConsumer.RemoteCall<{rtype.Name}>(\"{serviceName}\", \"{item.Key}\"{argsInput.ToString()});");
                    csStr.AppendLine(GetSpace(2) + "}");
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
            if (type.IsInterface) throw new Exception("不支持接口：" + TypeHelper.GetTypeName(type));

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
                    csStr.AppendLine($"{GetSpace(1)}public class {TypeHelper.GetTypeName(type)}");
                    csStr.AppendLine(GetSpace(1) + "{");
                    var ps = type.GetProperties();
                    foreach (var p in ps)
                    {
                        csStr.AppendLine($"{GetSpace(2)}public {TypeHelper.GetTypeName(p.PropertyType)} {p.Name}");
                        csStr.AppendLine(GetSpace(2) + "{");
                        csStr.AppendLine(GetSpace(3) + "get;set;");
                        csStr.AppendLine(GetSpace(2) + "}");
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
                csStr.AppendLine($"{GetSpace(1)}public enum {type.Name}:{baseType.Name}");
                csStr.AppendLine(GetSpace(1) + "{");
                var values = Enum.GetValues(type);
                foreach (var value in values)
                {
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
