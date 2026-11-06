using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    /// <summary>
    /// 新建目录对话框，校验目录名后通过 PathName 返回
    /// </summary>
    public partial class CreateDirForm : Form
    {
        /// <summary>
        /// 初始化新建目录对话框
        /// </summary>
        public CreateDirForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 用户输入的目录名称，仅在对话框返回 OK 后有值
        /// </summary>
        public string PathName { get; set; }

        private void skinButton1_Click(object sender, EventArgs e)
        {

            var pathName = skinWaterTextBox1.Text;

            if (string.IsNullOrWhiteSpace(pathName))
            {
                MessageBox.Show("名称不能为空！");
                return;
            }

            if (Path.GetInvalidPathChars().Any(b => pathName.Contains(b)))
            {
                MessageBox.Show("包含有非法的字符！");
                return;
            }
            this.PathName = pathName;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void skinButton2_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
