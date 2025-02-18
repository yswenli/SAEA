/****************************************************************************
*项目名称：SAEA.Common.Caching
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.Common.Caching
*类 名 称：ClassificationBatcher
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2020/12/17 15:53:02
*描述：
*=====================================================================
*修改时间：2020/12/17 15:53:02
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System;
using System.Collections.Concurrent;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 分类批量打包委托
    /// </summary>
    /// <param name="id"></param>
    /// <param name="data"></param>
    public delegate void OnClassificationBatchedHandler(string id, byte[] data);

    /// <summary>
    /// 分类批量打包类
    /// </summary>
    public sealed class ClassificationBatcher
    {
        ConcurrentDictionary<string, IBatcher> _dic;

        int _size, _timeout, _max;

        /// <summary>
        /// 分类批量打事件
        /// </summary>
        public event OnClassificationBatchedHandler OnBatched;

        /// <summary>
        /// 分类批量打包类
        /// </summary>
        /// <param name="size"></param>
        /// <param name="timeout"></param>
        /// <param name="max"></param>
        public ClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1)
        {
            _size = size;
            _timeout = timeout;
            if (max == -1) _max = _size * 10;
            if (_max < _size) throw new ArgumentOutOfRangeException("max不能小于size");
            _dic = new ConcurrentDictionary<string, IBatcher>();
        }


        #region static

        volatile static ClassificationBatcher _classificationBatcher = null;

        static object _locker = new object();

        /// <summary>
        /// 分类批量打包类
        /// </summary>
        /// <param name="size"></param>
        /// <param name="timeout"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static ClassificationBatcher GetInstance(int size = 1000, int timeout = 1000, int max = -1)
        {
            if (_classificationBatcher == null)
            {
                lock (_locker)
                {
                    if (_classificationBatcher == null)
                    {
                        _classificationBatcher = new ClassificationBatcher(size, timeout, max);
                    }
                }
            }
            return _classificationBatcher;
        }

        #endregion

        /// <summary>
        /// 插入
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public void Insert(string name, byte[] data)
        {
            if (_dic.TryGetValue(name, out IBatcher b))
            {
                if (b != null)
                {
                    var bacher = (Batcher)b;
                    bacher.Insert(data);
                }
            }
            else
            {
                var bacher = new Batcher(_size, _timeout, _max, name);
                bacher.OnBatched += Bacher_OnBatched;
                if (_dic.TryAdd(name, bacher))
                {
                    bacher.Insert(data);
                }
            }
        }

        /// <summary>
        /// 清理batcher
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        public void Clear(string name)
        {
            if (_dic.TryGetValue(name, out IBatcher b))
            {
                if (b != null)
                {
                    using (var bacher = (Batcher)b)
                    {
                        bacher.OnBatched -= Bacher_OnBatched;
                    }
                }
            }
        }

        private void Bacher_OnBatched(IBatcher sender, byte[] data)
        {
            var bacher = (Batcher)sender;

            OnBatched?.Invoke(bacher.Name, data);

        }
    }
}
