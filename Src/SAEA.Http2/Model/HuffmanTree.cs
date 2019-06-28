/****************************************************************************
*项目名称：SAEA.Http2.Model
*CLR 版本：4.0.30319.42000
*机器名称：WENLI-PC
*命名空间：SAEA.Http2.Model
*类 名 称：HuffmanTree
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2019/6/27 15:27:34
*描述：
*=====================================================================
*修改时间：2019/6/27 15:27:34
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using System;

namespace SAEA.Http2.Model
{
    /// <summary>
    /// 哈夫曼树
    /// </summary>
    static class HuffmanTree
    {
        /// <summary>
        /// 二进制哈夫曼树中的一个节点
        /// </summary>
        public class TreeNode
        {
            public TreeNode Child0;

            public TreeNode Child1;

            public int Value;
        }


        public static readonly TreeNode Root;

        static HuffmanTree()
        {

            Root = new TreeNode
            {
                Child0 = null,
                Child1 = null,
                Value = -1,
            };

            var i = 0;
            foreach (var entry in HuffmanTable.Entries)
            {
                InsertIntoTree(Root, i, entry.Bin, entry.Len);
                i++;
            }
        }

        private static void InsertIntoTree(TreeNode tree, int value, int bin, int len)
        {

            var firstBit = bin >> (len - 1);
            var child = (firstBit == 1) ? tree.Child1 : tree.Child0;
            if (len == 1)
            {

                if (child != null) throw new Exception("TreeNode alreay occupied");
                child = new TreeNode { Child0 = null, Child1 = null, Value = value };
                if (firstBit == 1) tree.Child1 = child;
                else tree.Child0 = child;
            }
            else
            {

                var rem = bin & ((1 << (len - 1)) - 1);
                if (child == null)
                {
                    child = new TreeNode { Child0 = null, Child1 = null, Value = -1 };
                    if (firstBit == 1) tree.Child1 = child;
                    else tree.Child0 = child;
                }

                InsertIntoTree(child, value, rem, len - 1);
            }
        }
    }
}
