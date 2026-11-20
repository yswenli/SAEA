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
*命名空间：SAEA.MVC.Routing
*文件名： RouteTable
*版本号： v26.4.23.1
*唯一标识：b12d2d02-7eb0-4743-9168-205c58375de6
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2018/10/28 14:19:50
*描述：RouteTable类
*
*=====================================================================
*修改标记
*修改时间：2026/11/20 14:59:51
*修改人： yswenli
*版本号： v26.11.16.1
*描述：控制器名索引拆成全名与裸名两张表并保留列表下标，两段式只认后缀；路由缓存键改为不分配的结构键；命中路径改为先查后建，不再每请求分配 GetOrAdd 闭包
*
*****************************************************************************/
using SAEA.Common;
using SAEA.MVC.Attributes;
using SAEA.MVC.Controllers;
using SAEA.MVC.Invocation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SAEA.MVC.Routing
{
    /// <summary>
    /// SAEA.MVC路由表
    /// </summary>
    public class RouteTable
    {
        // 键只用控制器类型名而不是 Type：旧实现按类型名拼键，同名不同类型会命中同一条目，
        // 换成 Type 会改变这种碰撞下的行为
        ConcurrentDictionary<(string ControllerName, string ActionName, string Method), RouteEntry> _cDic =
            new ConcurrentDictionary<(string ControllerName, string ActionName, string Method), RouteEntry>();

        static List<Type> _types = new List<Type>();

        // 控制器名索引，注册期一次性建好；构建完只读，请求期只做字典查找
        // _byFullName 键是类型全名，_byBareName 键是去掉结尾 Controller 的裸名，
        // 值里的下标用于跨两种名字形式复现「列表里第一个命中」
        static Dictionary<string, KeyValuePair<int, Type>> _byFullName;

        static Dictionary<string, KeyValuePair<int, Type>> _byBareName;

        // 建索引时的列表长度，用于发现外部绕过 setter 的原地追加
        static int _indexedTypeCount;

        /// <summary>
        /// 已扫描到的控制器类型集合
        /// </summary>
        public static List<Type> Types
        {
            get
            {
                return _types;
            }
            set
            {
                _types = value;

                RebuildControllerIndex();
            }
        }

        /// <summary>
        /// 按控制器名查类型：两种名字形式都认，同名时取列表里靠前的那个
        /// </summary>
        /// <param name="name">控制器名，可带或不带 Controller 后缀</param>
        /// <returns>列表顺序里第一个命中的类型；名字为空或未命中时返回 null</returns>
        internal Type FindController(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var types = _types;

            if (types == null || types.Count == 0) return null;

            EnsureControllerIndex(types.Count);

            var hasFullName = _byFullName.TryGetValue(name, out var fullNameEntry);

            var hasBareName = _byBareName.TryGetValue(name, out var bareNameEntry);

            // 两种形式都命中时比下标，复现「列表里第一个命中」
            if (hasFullName && hasBareName) return fullNameEntry.Key <= bareNameEntry.Key ? fullNameEntry.Value : bareNameEntry.Value;

            if (hasFullName) return fullNameEntry.Value;

            if (hasBareName) return bareNameEntry.Value;

            return null;
        }

        /// <summary>
        /// 按两段式约定查类型：只认「段名 + Controller」，不回退裸名
        /// </summary>
        /// <param name="name">地址里的控制器段名，不带 Controller 后缀</param>
        /// <returns>列表顺序里第一个后缀匹配的类型；名字为空或未命中时返回 null</returns>
        internal Type FindControllerBySuffix(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            var types = _types;

            if (types == null || types.Count == 0) return null;

            EnsureControllerIndex(types.Count);

            return _byBareName.TryGetValue(name, out var entry) ? entry.Value : null;
        }

        /// <summary>
        /// 索引缺失或列表长度变化时重建
        /// </summary>
        /// <param name="count">当前类型列表长度</param>
        static void EnsureControllerIndex(int count)
        {
            // 外部直接往 Types 里追加类型时不经过 setter，按列表长度不一致兜底重建
            if (_byFullName == null || _indexedTypeCount != count) RebuildControllerIndex();
        }

        /// <summary>
        /// 按列表顺序重建控制器名索引：一次遍历同时建全名表与裸名表，先到先得并记录列表下标
        /// </summary>
        static void RebuildControllerIndex()
        {
            var types = _types;

            var count = types == null ? 0 : types.Count;

            var byFullName = new Dictionary<string, KeyValuePair<int, Type>>(count, StringComparer.OrdinalIgnoreCase);

            var byBareName = new Dictionary<string, KeyValuePair<int, Type>>(count, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < count; i++)
            {
                var type = types[i];

                if (type == null) continue;

                var typeName = type.Name;

                AddControllerKey(byFullName, typeName, i, type);

                // 只有以 Controller 结尾的类型才进裸名表，裸名可能为空时不写
                if (typeName.EndsWith(ConstHelper.CONTROLLERNAME, StringComparison.OrdinalIgnoreCase))
                {
                    AddControllerKey(byBareName, typeName.Substring(0, typeName.Length - ConstHelper.CONTROLLERNAME.Length), i, type);
                }
            }

            // 先建好两张新表再赋值：读侧即使读到半新半旧，两张表也来自同一份列表快照
            _byFullName = byFullName;

            _byBareName = byBareName;

            _indexedTypeCount = count;
        }

        /// <summary>
        /// 写入一个索引键，已存在时不覆盖，保留列表里靠前的类型
        /// </summary>
        /// <param name="map">待写入的索引</param>
        /// <param name="key">控制器名</param>
        /// <param name="index">该类型在 Types 里的下标</param>
        /// <param name="type">对应的控制器类型</param>
        static void AddControllerKey(Dictionary<string, KeyValuePair<int, Type>> map, string key, int index, Type type)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (!map.ContainsKey(key)) map[key] = new KeyValuePair<int, Type>(index, type);
        }

        /// <summary>
        /// 获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="isPost">是否为 POST 请求</param>
        /// <returns>匹配到的路由；找不到 action 或 controllerType 为 null 时返回 null</returns>
        public RouteEntry GetOrAdd(Type controllerType, string actionName, bool isPost)
        {
            return GetOrAdd(controllerType, actionName, isPost ? ConstHelper.POST : ConstHelper.GET);
        }

        /// <summary>
        /// 按请求方法获取routing中的缓存
        /// 若不存在则创建
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="method">请求方法，如 GET；为空时按 GET 处理</param>
        /// <returns>匹配到的路由；找不到 action 或 controllerType 为 null 时返回 null</returns>
        public RouteEntry GetOrAdd(Type controllerType, string actionName, string method)
        {
            if (controllerType == null) return null;

            // 未给出方法时按 GET 处理，保持旧的重载与空方法调用等价
            if (string.IsNullOrEmpty(method)) method = ConstHelper.GET;

            // 键是值元组，不装箱也不分配；控制器名取 Name 而不是 Type，保持旧键的碰撞语义
            var key = (ControllerName: controllerType.Name, ActionName: actionName, Method: method);

            // 先查后建：GetOrAdd 的工厂闭包会捕获 this 与 4 个局部量，命中缓存时工厂根本不执行，
            // 闭包与委托属于纯白付；先判一次只多付一次字典查找，慢路径仍保留原来的 GetOrAdd 语义。
            // TryGetValue 对缓存值为 null 的键同样返回 true：未知 action 的 404 结果就是缓存的 null，
            // 命中时必须原样返回（含 null），不能因为值是 null 就落到慢路径重建
            if (_cDic.TryGetValue(key, out var cached)) return cached;

            // 未命中时才为当前请求动词的键做一次 GetOrAdd：未声明动词的 action 由工厂补写全部动词的键，
            // 声明了动词的 action 只落回自己声明的键；解析为空时工厂返回 null 并作为该键的缓存值，后续请求直接命中
            return _cDic.GetOrAdd(key, (k) => BuildEntry(controllerType, actionName, method, key));
        }

        /// <summary>
        /// 解析 action 并构造当前动词的路由条目：未声明动词时按 HttpMethodCatalog.All 补写全部动词的键，
        /// 声明了动词时只写当前动词的键
        /// </summary>
        /// <param name="controllerType">控制器类型</param>
        /// <param name="actionName">动作名称</param>
        /// <param name="method">请求方法，决定 action 的解析范围与条目的动词标记</param>
        /// <param name="key">当前动词的路由缓存键</param>
        /// <returns>当前动词对应的路由条目；找不到 action 时返回 null</returns>
        RouteEntry BuildEntry(Type controllerType, string actionName, string method, (string ControllerName, string ActionName, string Method) key)
        {
            var actions = ActionResolver.Resolve(controllerType, actionName, method);

            if (actions == null || actions.Count == 0)
            {
                // 未知 action 属于请求错误，返回 null 交给上层回 404；GetOrAdd 会把 null 作为该键的缓存值，后续请求直接命中
                return null;
            }

            if (actions.Count > 2)
            {
                throw new Exception($"{controllerType.Name}/{actionName}有多个重复的的action!");
            }

            // 一次解析只创建一个控制器实例，同一 action 的各个动词条目共用
            var instance = Activator.CreateInstance(controllerType);

            var classFilters = FilterCollector.CollectClassFilters(controllerType);

            RouteEntry entry = null;

            foreach (var action in actions)
            {
                entry = new RouteEntry()
                {
                    ControllerName = controllerType.Name,
                    ActionName = actionName,
                    Instance = (Controller)instance,
                    FilterAtrrs = classFilters,
                    Action = action,
                    IsPost = string.Compare(method, ConstHelper.POST, true) == 0
                };

                var actionFilters = FilterCollector.CollectActionFilters(action);

                if (actionFilters != null)
                {
                    entry.ActionFilterAtrrs = actionFilters;
                }

                var methodAttributes = HttpMethodCatalog.FromAttributes(action.GetCustomAttributes(true));

                if (methodAttributes.Length > 0)
                {
                    // 声明了动词特性时只写命中的键：未声明的动词留给它自己的解析，不提前固化别的动词的条目；
                    // 即使当前动词未被声明也返回非 null 条目，让上层按声明给出 405 而不是 404
                    for (var i = 0; i < methodAttributes.Length; i++)
                    {
                        if (string.Compare(methodAttributes[i].Method, method, true) != 0) continue;

                        // 这次写入与外层 GetOrAdd 落在同一个键上，但不能省：同一 action 名解析出两个重载且都声明
                        // 当前动词时，先遍历到的重载在这里落键，外层 GetOrAdd 不覆盖已有值，覆盖语义由这次写入决定
                        _cDic.TryAdd(key, entry);
                    }
                }
                else
                {
                    // 未声明动词时对全部已知动词放行，按 All 补写缓存键
                    for (var i = 0; i < HttpMethodCatalog.All.Length; i++)
                    {
                        _cDic.TryAdd((controllerType.Name, actionName, HttpMethodCatalog.All[i]), entry);
                    }
                }
            }

            return entry;
        }
    }

}