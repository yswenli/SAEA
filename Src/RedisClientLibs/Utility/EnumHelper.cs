/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.Commom
*文件名： EnumHelper
*版本号： V1.0.0.0
*唯一标识：0957f3bb-7462-4ff0-867d-0a8c9411f2eb
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/4/12 9:33:39
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/4/12 9:33:39
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace SAEA.Common
{
    public static class EnumHelper
    {
        /// <summary>
        /// 获取DescriptionAttribute
        /// </summary>
        /// <param name="enum"></param>
        /// <returns></returns>
        public static string GetDescription(this Enum @enum)
        {
            var result = string.Empty;

            var typeInfo = @enum.GetType();

            var enumValues = typeInfo.GetEnumValues();

            foreach (var value in enumValues)
            {
                if (@enum.Equals(value))
                {
                    MemberInfo memberInfo = typeInfo.GetMember(value.ToString()).First();

                    result = memberInfo.GetCustomAttribute<DescriptionAttribute>().Description;
                }
            }
            return result;
        }

        public static string ToNVString(this HttpStatusCode @enum)
        {
            return $"{(int)@enum}  {@enum.ToString()}";
        }

    }
}
