using System;
using System.Collections.Concurrent;

namespace SAEA.Common.Caching
{
    /// <summary>
    /// 按 id 分类的池化批处理器。每个 id 一个 <see cref="PooledBatcher"/>，回调携带 id。
    /// </summary>
    public delegate void OnPooledClassificationBatchedHandler(string id, PooledBufferWriter batch, int count);

    /// <summary>
    /// 池化分类批处理器，替代全局单例 <see cref="ClassificationBatcher"/> 以解除跨子系统参数耦合。
    /// </summary>
    public sealed class PooledClassificationBatcher : IDisposable
    {
        readonly ConcurrentDictionary<string, PooledBatcher> _dic;
        readonly int _size;
        readonly int _timeout;
        readonly int _max;

        public event OnPooledClassificationBatchedHandler OnBatched;

        public PooledClassificationBatcher(int size = 1000, int timeout = 1000, int max = -1)
        {
            _size = size > 0 ? size : 1000;
            _timeout = timeout > 0 ? timeout : 1000;
            _max = max == -1 ? _size * 10 : max;
            if (_max < _size) throw new ArgumentOutOfRangeException(nameof(max), "max不能小于size");
            _dic = new ConcurrentDictionary<string, PooledBatcher>();
        }

        PooledBatcher GetOrCreate(string id)
        {
            return _dic.GetOrAdd(id, n =>
            {
                var b = new PooledBatcher(_size, _timeout, _max, n);
                var captured = n;
                b.OnBatched += (w, count) =>
                {
                    var h = OnBatched;
                    if (h != null) h(captured, w, count);
                    else w.Dispose();
                };
                return b;
            });
        }

        public bool Insert(string id, PooledBufferWriter writer)
        {
            if (string.IsNullOrEmpty(id) || writer == null) return false;
            return GetOrCreate(id).Insert(writer);
        }

        public void Clear(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_dic.TryRemove(id, out var b)) b.Dispose();
        }

        public void Dispose()
        {
            foreach (var kv in _dic) { try { kv.Value.Dispose(); } catch { } }
            _dic.Clear();
        }
    }
}
