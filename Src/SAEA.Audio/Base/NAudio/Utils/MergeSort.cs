using System;
using System.Collections.Generic;

namespace SAEA.Audio.Base.NAudio.Utils
{
	internal class MergeSort
	{
		private static void Sort<T>(IList<T> list, int lowIndex, int highIndex, IComparer<T> comparer)
		{
			if (lowIndex >= highIndex)
			{
				return;
			}
			int num = (lowIndex + highIndex) / 2;
			MergeSort.Sort<T>(list, lowIndex, num, comparer);
			MergeSort.Sort<T>(list, num + 1, highIndex, comparer);
			int num2 = num;
			int num3 = num + 1;
			while (lowIndex <= num2 && num3 <= highIndex)
			{
				if (comparer.Compare(list[lowIndex], list[num3]) <= 0)
				{
					lowIndex++;
				}
				else
				{
					T value = list[num3];
					for (int i = num3 - 1; i >= lowIndex; i--)
					{
						list[i + 1] = list[i];
					}
					list[lowIndex] = value;
					lowIndex++;
					num2++;
					num3++;
				}
			}
		}

		public static void Sort<T>(IList<T> list) where T : IComparable<T>
		{
			MergeSort.Sort<T>(list, 0, list.Count - 1, Comparer<T>.Default);
		}

		public static void Sort<T>(IList<T> list, IComparer<T> comparer)
		{
			MergeSort.Sort<T>(list, 0, list.Count - 1, comparer);
		}
	}
}
