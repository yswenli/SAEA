/****************************************************************************
*项目名称：SAEA.RedisSocket.Core.Stream
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.RedisSocket.Core.Stream
*类 名 称：RedisField
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/1/7 19:39:51
*描述：
*=====================================================================
*修改时间：2021/1/7 19:39:51
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/

namespace SAEA.RedisSocket.Core.Stream
{
    /// <summary>
    /// RedisField
    /// </summary>
    public class RedisField
    {
        string _field = string.Empty;

        public string Field
        {
            get
            {
                return _field;
            }
            set
            {
                _field = value?.TrimEnd();
            }
        }

        string _str = string.Empty;

        public string String
        {
            get
            {
                return _str;
            }
            set
            {
                _str = value?.TrimEnd();
            }
        }

        /// <summary>
        /// RedisField
        /// </summary>
        public RedisField() { }

        /// <summary>
        /// RedisField
        /// </summary>
        /// <param name="field"></param>
        /// <param name="str"></param>
        public RedisField(string field, string str)
        {
            Field = field;
            String = str;
        }
    }
}
