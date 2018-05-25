/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Generater
*文件名： CodeGnerater
*版本号： V1.0.0.0
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
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.RPC.Common;
using SAEA.RPC.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SAEA.RPC.Generater
{
    /// <summary>
    /// 代码生成器
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
            return "_" + str.Substring(0, 1).ToLower() + str.Substring(1);
        }

        /// <summary>
        /// 生成代码头部
        /// </summary>
        /// <returns></returns>
        static string Header(params string[] usings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("/*******");
            sb.AppendLine($"*此代码为SAEA.RPCGenerater生成 {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}");
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
            csStr.AppendLine(Header("using SAEA.RPC.Consumer;", $"using {spaceName}.Consumer.Model;", $"using {spaceName}.Consumer.Service;"));
            csStr.AppendLine($"namespace {spaceName}.Consumer");
            csStr.AppendLine("{");
            csStr.AppendLine($"{GetSpace(1)}public class RPCServiceProxy");
            csStr.AppendLine(GetSpace(1) + "{");

            csStr.AppendLine(GetSpace(2) + "ServiceConsumer _serviceConsumer;");
            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(string uri = \"rpc://127.0.0.1:39654\") : this(new Uri(uri)){}");
            csStr.AppendLine(GetSpace(2) + "public RPCServiceProxy(Uri uri)");
            csStr.AppendLine(GetSpace(2) + "{");

            csStr.AppendLine(GetSpace(3) + "_serviceConsumer = new ServiceConsumer(uri);");

            var names = RPCMapping.GetServiceNames();

            if (names != null)
            {
                foreach (var name in names)
                {
                    csStr.AppendLine(GetSpace(3) + GetSuffixStr(name) + $" = new {name}(_serviceConsumer);");
                }
            }
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
                var rtype = item.Value.Mothd.ReturnType;

                if (rtype != null)
                {
                    if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{rtype.Name}"))
                    {
                        GenerateModel(spaceName, rtype);
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
                        argsStr.Append(arg.Value.Name);
                        argsStr.Append(" ");
                        argsStr.Append(arg.Key);
                        if (i < item.Value.Pamars.Count)
                            argsStr.Append(", ");

                        if (arg.Value != null && arg.Value.IsClass)
                        {
                            if (!_modelStrs.ContainsKey($"{spaceName}.Consumer.Model.{arg.Value.Name}"))
                            {
                                GenerateModel(spaceName, arg.Value);
                            }
                        }

                        argsInput.Append(", ");
                        argsInput.Append(arg.Key);
                    }
                }

                csStr.AppendLine(GetSpace(2) + $"public {rtype.Name} {item.Key}({argsStr.ToString()})");
                csStr.AppendLine(GetSpace(2) + "{");
                csStr.AppendLine(GetSpace(3) + $"return _serviceConsumer.RemoteCall<{rtype.Name}>(\"{serviceName}\", \"{item.Key}\"{argsInput.ToString()});");
                csStr.AppendLine(GetSpace(2) + "}");


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
        internal static void GenerateModel(string spaceName, Type type)
        {
            if (!IsModel(type)) return;
            StringBuilder csStr = new StringBuilder();
            csStr.AppendLine($"namespace {spaceName}.Consumer.Model");
            csStr.AppendLine("{");
            csStr.AppendLine($"{GetSpace(1)}public class {type.Name}");
            csStr.AppendLine(GetSpace(1) + "{");
            var ps = type.GetProperties();
            foreach (var p in ps)
            {
                csStr.AppendLine($"{GetSpace(2)}public {p.PropertyType.Name} {p.Name}");
                csStr.AppendLine(GetSpace(2) + "{");
                csStr.AppendLine(GetSpace(3) + "get;set;");
                csStr.AppendLine(GetSpace(2) + "}");
            }
            csStr.AppendLine(GetSpace(1) + "}");
            csStr.AppendLine("}");
            _modelStrs.Add($"{spaceName}.Consumer.Model.{type.Name}", csStr.ToString());
        }

        /// <summary>
        /// 是否是实体
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static bool IsModel(Type type)
        {
            if (type.IsArray || type.IsSealed || !type.IsClass)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 生成客户端C#代码文件
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="spaceName"></param>
        public static void Generate(string folder, string spaceName)
        {
            RPCMapping.RegistAll();

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
