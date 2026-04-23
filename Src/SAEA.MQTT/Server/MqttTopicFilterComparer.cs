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
*命名空间：SAEA.MQTT.Server
*文件名： MqttTopicFilterComparer
*版本号： v26.4.23.1
*唯一标识：a7d1aac0-180b-47d3-9afd-aa11e3ab04a9
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2021/03/11 16:46:45
*描述：MQTT服务端类
*
*=====================================================================
*修改标记
*修改时间：2021/03/11 16:46:45
*修改人： yswenli
*版本号： v26.4.23.1
*描述：MQTT服务端类
*
*****************************************************************************/
using System;

namespace SAEA.MQTT.Server
{
    public static class MqttTopicFilterComparer
    {
        const char LevelSeparator = '/';
        const char MultiLevelWildcard = '#';
        const char SingleLevelWildcard = '+';

        public static bool IsMatch(string topic, string filter)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            var sPos = 0;
            var sLen = filter.Length;
            var tPos = 0;
            var tLen = topic.Length;

            while (sPos < sLen && tPos < tLen)
            {
                if (filter[sPos] == topic[tPos])
                {
                    if (tPos == tLen - 1)
                    {
                        // Check for e.g. foo matching foo/#
                        if (sPos == sLen - 3
                                && filter[sPos + 1] == LevelSeparator
                                && filter[sPos + 2] == MultiLevelWildcard)
                        {
                            return true;
                        }
                    }

                    sPos++;
                    tPos++;

                    if (sPos == sLen && tPos == tLen)
                    {
                        return true;
                    }

                    if (tPos == tLen && sPos == sLen - 1 && filter[sPos] == SingleLevelWildcard)
                    {
                        if (sPos > 0 && filter[sPos - 1] != LevelSeparator)
                        {
                            // Invalid filter string
                            return false;
                        }

                        return true;
                    }
                }
                else
                {
                    if (filter[sPos] == SingleLevelWildcard)
                    {
                        // Check for bad "+foo" or "a/+foo" subscription
                        if (sPos > 0 && filter[sPos - 1] != LevelSeparator)
                        {
                            // Invalid filter string
                            return false;
                        }

                        // Check for bad "foo+" or "foo+/a" subscription
                        if (sPos < sLen - 1 && filter[sPos + 1] != LevelSeparator)
                        {
                            // Invalid filter string
                            return false;
                        }

                        sPos++;
                        while (tPos < tLen && topic[tPos] != LevelSeparator)
                        {
                            tPos++;
                        }

                        if (tPos == tLen && sPos == sLen)
                        {
                            return true;
                        }
                    }
                    else if (filter[sPos] == MultiLevelWildcard)
                    {
                        if (sPos > 0 && filter[sPos - 1] != LevelSeparator)
                        {
                            // Invalid filter string
                            return false;
                        }

                        if (sPos + 1 != sLen)
                        {
                            // Invalid filter string
                            return false;
                        }

                        return true;
                    }
                    else
                    {
                        // Check for e.g. foo/bar matching foo/+/#
                        if (sPos > 0
                                && sPos + 2 == sLen
                                && tPos == tLen
                                && filter[sPos - 1] == SingleLevelWildcard
                                && filter[sPos] == LevelSeparator
                                && filter[sPos + 1] == MultiLevelWildcard)
                        {
                            return true;
                        }

                        return false;
                    }
                }
            }

            return false;
        }
    }
}
