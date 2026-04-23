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
*命名空间：SAEA.MVC
*文件名： SAEAMvcApplicationConfigBuilder
*版本号： v26.4.23.1
*唯一标识：4c8b3cd2-6df9-4cb0-b4e1-087c443f07d2
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/01/22 15:08:29
*描述：
*
*=====================================================================
*修改标记
*修改时间：2019/01/22 15:08:29
*修改人： yswenli
*版本号： v26.4.23.1
*描述：
*
*****************************************************************************/
using SAEA.Common;
using SAEA.Common.IO;
using SAEA.Common.Serialization;
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