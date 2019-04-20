/****************************************************************************
*Copyright (c) 2018 yswenli All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：yswenli
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplicationConfigBuilder
*版本号： v4.3.3.7
*唯一标识：1ed5d381-d7ce-4ea3-b8b5-c32f581ad49f
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 10:55:31
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 10:55:31
*修改人： yswenli
*版本号： v4.3.3.7
*描述：
*
*****************************************************************************/
using SAEA.Common;
using System;

namespace SAEA.MVC
{
    public static class SAEAMvcApplicationConfigBuilder
    {
        /// <summary>
        /// 读取SAEAMvcApplicationConfig.json配置
        /// </summary>
        /// <returns></returns>
        public static SAEAMvcApplicationConfig Read()
        {
            SAEAMvcApplicationConfig result;

            string filePath = string.Empty;
            try
            {
                filePath = PathHelper.GetFullName("SAEAMvcApplicationConfig.json");

                if (!FileHelper.Exists(filePath))
                {
                    result = SAEAMvcApplicationConfig.Default;

                    Write(result);
                }
                else
                {
                    result = SerializeHelper.Deserialize<SAEAMvcApplicationConfig>(FileHelper.ReadString(filePath));
                }

            }
            catch (Exception ex)
            {
                result = SAEAMvcApplicationConfig.Default;

                LogHelper.Error("SAEAMvcApplicationConfigBuilder.Read", ex);
            }
            return result;
        }

        /// <summary>
        /// 写入SAEAMvcApplicationConfig.json配置
        /// </summary>
        /// <param name="config"></param>
        public static void Write(SAEAMvcApplicationConfig config)
        {
            if (config == null) return;

            string json = string.Empty;

            string filePath = string.Empty;

            try
            {
                filePath = PathHelper.GetFullName("SAEAMvcApplicationConfig.json");

                json = SerializeHelper.Serialize(config);

                FileHelper.WriteString(filePath, json);
            }
            catch (Exception ex)
            {
                LogHelper.Error("SAEAMvcApplicationConfigBuilder.Write ", ex);
            }

        }
    }
}
