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
*命名空间：SAEA.RedisSocket.Core.Stream
*文件名： RedisField
*版本号： v26.4.23.1
*唯一标识：c4fe97dd-355c-4320-8557-d49283bd82a3
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/01/08 20:00:21
*描述：RedisField接口
*
*=====================================================================
*修改标记
*修改时间：2021/01/08 20:00:21
*修改人： yswenli
*版本号： v26.4.23.1
*描述：RedisField接口
*
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
