/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：StreamEntry
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/9 10:10:59
*描述：
*=====================================================================
*修改时间：2021/1/9 10:10:59
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using System.Collections.Generic;

namespace SAEA.RedisSocket.Core.Stream
{
    public class StreamEntry
    {
        string _topic;
        public string Topic {
            get
            {
                return _topic;
            }
            set
            {
                _topic = value?.TrimEnd();
            }
        }

        public IEnumerable<IdFiled> IdFileds { get; set; }
    }

    public class IdFiled
    {
        public RedisID RedisID { get; set; }

        public IEnumerable<RedisField> RedisFields { get; set; }
    }
}
