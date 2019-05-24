/****************************************************************************
*项目名称：SAEA.Mongo.Bson
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Mongo.Bson
*类 名 称：Hasher
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/5/22 11:10:42
*描述：
*=====================================================================
*修改时间：2019/5/22 11:10:42
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SAEA.Mongo.Bson
{
    internal class Hasher
    {
        // private fields
        private int _hashCode;

        // constructors
        public Hasher()
        {
            _hashCode = 17;
        }

        public Hasher(int seed)
        {
            _hashCode = seed;
        }

        // public methods
        public override int GetHashCode()
        {
            return _hashCode;
        }

        // this overload added to avoid boxing
        public Hasher Hash(bool obj)
        {
            _hashCode = 37 * _hashCode + obj.GetHashCode();
            return this;
        }

        // this overload added to avoid boxing
        public Hasher Hash(int obj)
        {
            _hashCode = 37 * _hashCode + obj.GetHashCode();
            return this;
        }

        // this overload added to avoid boxing
        public Hasher Hash(long obj)
        {
            _hashCode = 37 * _hashCode + obj.GetHashCode();
            return this;
        }

        // this overload added to avoid boxing
        public Hasher Hash<T>(Nullable<T> obj) where T : struct
        {
            _hashCode = 37 * _hashCode + ((obj == null) ? -1 : obj.Value.GetHashCode());
            return this;
        }

        public Hasher Hash(object obj)
        {
            _hashCode = 37 * _hashCode + ((obj == null) ? -1 : obj.GetHashCode());
            return this;
        }

        public Hasher HashElements(IEnumerable sequence)
        {
            if (sequence == null)
            {
                _hashCode = 37 * _hashCode + -1;
            }
            else
            {
                foreach (var value in sequence)
                {
                    _hashCode = 37 * _hashCode + ((value == null) ? -1 : value.GetHashCode());
                }
            }
            return this;
        }

        public Hasher HashStructElements<T>(IEnumerable<T> sequence) where T : struct
        {
            foreach (var value in sequence)
            {
                _hashCode = 37 * _hashCode + value.GetHashCode();
            }
            return this;
        }
    }
}
