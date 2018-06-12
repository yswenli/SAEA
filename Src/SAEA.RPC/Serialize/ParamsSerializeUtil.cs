using SAEA.Commom;
using SAEA.RPC.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace SAEA.RPC.Serialize
{
    /// <summary>
    /// rpc参数序列化处理
    /// </summary>
    public class ParamsSerializeUtil
    {
        /// <summary>
        /// len+data
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        public static byte[] Serialize(object param)
        {
            List<byte> datas = new List<byte>();

            var len = 0;
            byte[] data = null;

            if (param == null)
            {
                len = 0;
            }
            else
            {
                if (param is string)
                {
                    data = Encoding.UTF8.GetBytes((string)param);
                }
                else if (param is byte)
                {
                    data = new byte[] { (byte)param };
                }
                else if (param is bool)
                {
                    data = BitConverter.GetBytes((bool)param);
                }
                else if (param is short)
                {
                    data = BitConverter.GetBytes((short)param);
                }
                else if (param is int)
                {
                    data = BitConverter.GetBytes((int)param);
                }
                else if (param is long)
                {
                    data = BitConverter.GetBytes((long)param);
                }
                else if (param is float)
                {
                    data = BitConverter.GetBytes((float)param);
                }
                else if (param is double)
                {
                    data = BitConverter.GetBytes((double)param);
                }
                else if (param is DateTime)
                {
                    var str = "wl" + ((DateTime)param).Ticks;
                    data = Encoding.UTF8.GetBytes(str);
                }
                else if (param is byte[])
                {
                    data = (byte[])param;
                }
                else
                {
                    var type = param.GetType();

                    if (type.IsClass)
                    {
                        if (type.IsGenericType || type.IsArray)
                        {
                            if (type.Name == "Dictionary`2")
                                data = SerializeDic((System.Collections.IDictionary)param);
                            else if (type.Name == "List`1")
                                data = SerializeList((System.Collections.IEnumerable)param);
                            else
                                data = SerializeClass(param);
                        }
                        else
                            data = SerializeClass(param);
                    }
                }
                if (data != null)
                    len = data.Length;
            }
            datas.AddRange(BitConverter.GetBytes(len));
            if (len > 0)
            {
                datas.AddRange(data);
            }
            return datas.Count == 0 ? null : datas.ToArray();
        }

        private static byte[] SerializeClass(object param)
        {
            List<byte> datas = new List<byte>();

            var len = 0;

            byte[] data = null;

            var type = param.GetType();

            var ps = type.GetProperties();

            if (ps != null && ps.Length > 0)
            {
                List<object> clist = new List<object>();

                foreach (var p in ps)
                {
                    clist.Add(p.GetValue(param, null));
                }
                data = Serialize(clist.ToArray());

                len = data.Length;
            }
            if (len > 0)
            {
                return data;
            }
            return null;
        }

        private static byte[] SerializeList(System.Collections.IEnumerable param)
        {
            if (param != null)
            {
                List<byte> slist = new List<byte>();

                foreach (var item in param)
                {
                    var type = item.GetType();

                    var ps = type.GetProperties();
                    if (ps != null && ps.Length > 0)
                    {
                        List<object> clist = new List<object>();
                        foreach (var p in ps)
                        {
                            clist.Add(p.GetValue(item, null));
                        }

                        var clen = 0;

                        var cdata = Serialize(clist.ToArray());

                        if (cdata != null)
                        {
                            clen = cdata.Length;
                        }

                        slist.AddRange(BitConverter.GetBytes(clen));
                        slist.AddRange(cdata);
                    }
                }

                if (slist.Count > 0)
                {
                    return slist.ToArray();
                }
            }
            return null;
        }

        private static byte[] SerializeDic(System.Collections.IDictionary param)
        {
            if (param != null && param.Count > 0)
            {
                foreach (var item in param)
                {
                    var type = item.GetType();
                    var ps = type.GetProperties();
                    if (ps != null && ps.Length > 0)
                    {
                        List<object> clist = new List<object>();
                        foreach (var p in ps)
                        {
                            clist.Add(p.GetValue(item, null));
                        }
                        var clen = 0;

                        var cdata = Serialize(clist.ToArray());

                        if (cdata != null)
                        {
                            clen = cdata.Length;
                        }

                        if (clen > 0)
                        {
                            return cdata;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// len+data
        /// </summary>
        /// <param name="params"></param>
        /// <returns></returns>
        public static byte[] Serialize(params object[] @params)
        {
            List<byte> datas = new List<byte>();

            if (@params != null)
            {
                foreach (var param in @params)
                {
                    datas.AddRange(Serialize(param));
                }
            }

            return datas.Count == 0 ? null : datas.ToArray();
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="types"></param>
        /// <param name="datas"></param>
        /// <returns></returns>
        public static object[] Deserialize(Type[] types, byte[] datas)
        {
            List<object> list = new List<object>();

            int offset = 0;

            for (int i = 0; i < types.Length; i++)
            {
                list.Add(Deserialize(types[i], datas, ref offset));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="type"></param>
        /// <param name="datas"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static object Deserialize(Type type, byte[] datas, ref int offset)
        {
            dynamic obj = null;

            var len = 0;

            byte[] data = null;

            len = BitConverter.ToInt32(datas, offset);
            offset += 4;
            if (len > 0)
            {
                data = new byte[len];
                Buffer.BlockCopy(datas, offset, data, 0, len);
                offset += len;

                if (type == typeof(string))
                {
                    obj = Encoding.UTF8.GetString(data);
                }
                else if (type == typeof(byte))
                {
                    obj = (data);
                }
                else if (type == typeof(bool))
                {
                    obj = (BitConverter.ToBoolean(data, 0));
                }
                else if (type == typeof(short))
                {
                    obj = (BitConverter.ToInt16(data, 0));
                }
                else if (type == typeof(int))
                {
                    obj = (BitConverter.ToInt32(data, 0));
                }
                else if (type == typeof(long))
                {
                    obj = (BitConverter.ToInt64(data, 0));
                }
                else if (type == typeof(float))
                {
                    obj = (BitConverter.ToSingle(data, 0));
                }
                else if (type == typeof(double))
                {
                    obj = (BitConverter.ToDouble(data, 0));
                }
                else if (type == typeof(decimal))
                {
                    obj = (BitConverter.ToDouble(data, 0));
                }
                else if (type == typeof(DateTime))
                {
                    var dstr = Encoding.UTF8.GetString(data);
                    var ticks = long.Parse(dstr.Substring(2));
                    obj = (new DateTime(ticks));
                }
                else if (type == typeof(byte[]))
                {
                    obj = (byte[])data;
                }
                else if (type.IsGenericType)
                {
                    if (type.Name == "List`1")
                    {
                        obj = DeserializeList(type, data);
                    }
                    else if (type.Name == "Dictionary`2")
                    {
                        obj = DeserializeDic(type, data);
                    }
                    else
                    {
                        obj = DeserializeClass(type, data);
                    }
                }
                else if (type.IsArray)
                {
                    obj = DeserializeArray(type, data);
                }
                else if (type.IsClass)
                {
                    obj = DeserializeClass(type, data);
                }
                else
                {
                    throw new RPCPamarsException("ParamsSerializeUtil.Deserialize 未定义的类型：" + type.ToString());
                }

            }
            return obj;
        }

        private static object DeserializeClass(Type type, byte[] datas)
        {
            var instance = Activator.CreateInstance(type);

            var ts = new List<Type>();

            var ps = type.GetProperties();

            if (ps != null)
            {
                foreach (var p in ps)
                {
                    ts.Add(p.PropertyType);
                }

                var vas = Deserialize(ts.ToArray(), datas);

                for (int j = 0; j < ps.Length; j++)
                {
                    try
                    {
                        if (!ps[j].PropertyType.IsGenericType)
                        {
                            ps[j].SetValue(instance, Convert.ChangeType(vas[j], ps[j].PropertyType), null);
                        }
                        else
                        {
                            Type genericTypeDefinition = ps[j].PropertyType.GetGenericTypeDefinition();
                            if (genericTypeDefinition == typeof(Nullable<>))
                            {
                                ps[j].SetValue(instance, Convert.ChangeType(vas[j], Nullable.GetUnderlyingType(ps[j].PropertyType)), null);
                            }
                            else
                            {
                                ps[j].SetValue(instance, Convert.ChangeType(vas[j], ps[j].PropertyType), null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("反序列化不支持的类型：" + ex.Message);
                    }
                }
            }
            return instance;
        }

        private static object DeserializeList(Type type, byte[] datas)
        {
            var stype = type.GetGenericArguments()[0];

            var gtype = type.GetGenericTypeDefinition().MakeGenericType(stype);

            var result = Activator.CreateInstance(gtype);

            var addMethod = gtype.GetMethod("Add");

            var methodInvoker = FastInvoke.GetMethodInvoker(addMethod);

            //子项内容
            var slen = 0;
            var soffset = 0;
            while (soffset < datas.Length)
            {
                slen = BitConverter.ToInt32(datas, soffset);
                var sdata = new byte[slen + 4];
                Buffer.BlockCopy(datas, soffset, sdata, 0, slen + 4);
                soffset += slen + 4;

                if (slen > 0)
                {
                    int lloffset = 0;
                    var sobj = Deserialize(stype, sdata, ref lloffset);
                    if (sobj != null)
                        methodInvoker.Invoke(result, new object[] { sobj });
                }
                else
                {
                    methodInvoker.Invoke(result, null);
                }
            }
            return result;
        }

        private static object DeserializeArray(Type type, byte[] datas)
        {
            var obj = DeserializeList(type, datas);

            if (obj == null) return null;

            var list = (obj as List<object>);

            return list.ToArray();
        }

        private static object DeserializeDic(Type type, byte[] datas)
        {
            var stype1 = type.GetGenericArguments()[0];

            var stype2 = type.GetGenericArguments()[1];

            var gtype = type.GetGenericTypeDefinition().MakeGenericType(stype1, stype2);

            var result = Activator.CreateInstance(gtype);

            var addMethod = gtype.GetMethod("Add");

            var methodInvoker = FastInvoke.GetMethodInvoker(addMethod);
                       
            //子项内容
            var slen = 0;
            var soffset = 0;

            int m = 1;

            object key = null;
            object val = null;

            while (soffset < datas.Length)
            {
                slen = BitConverter.ToInt32(datas, soffset);
                var sdata = new byte[slen + 4];
                Buffer.BlockCopy(datas, soffset, sdata, 0, slen + 4);
                soffset += slen + 4;
                if (m % 2 == 1)
                {
                    object v = null;
                    if (slen > 0)
                    {
                        int lloffset = 0;
                        var sobj = Deserialize(stype1, sdata, ref lloffset);
                        if (sobj != null)
                            v = sobj;
                    }
                    key = v;
                }
                else
                {
                    object v = null;
                    if (slen > 0)
                    {
                        int lloffset = 0;
                        var sobj = Deserialize(stype2, sdata, ref lloffset);
                        if (sobj != null)
                            v = sobj;
                    }
                    val = v;
                    methodInvoker.Invoke(result, new object[] { key, val });
                }
                m++;
            }
            return result;
        }
    }
}
