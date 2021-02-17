using System;
using System.Reflection;

namespace SAEA.Audio.Base.NAudio.Utils
{
	public static class FieldDescriptionHelper
	{
		public static string Describe(Type t, Guid guid)
		{
			FieldInfo[] fields = t.GetFields(BindingFlags.Static | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			{
				FieldInfo fieldInfo = fields[i];
				if (fieldInfo.IsPublic && fieldInfo.IsStatic && fieldInfo.FieldType == typeof(Guid) && (Guid)fieldInfo.GetValue(null) == guid)
				{
					object[] customAttributes = fieldInfo.GetCustomAttributes(false);
					for (int j = 0; j < customAttributes.Length; j++)
					{
						FieldDescriptionAttribute fieldDescriptionAttribute = customAttributes[j] as FieldDescriptionAttribute;
						if (fieldDescriptionAttribute != null)
						{
							return fieldDescriptionAttribute.Description;
						}
					}
					return fieldInfo.Name;
				}
			}
			return guid.ToString();
		}
	}
}
