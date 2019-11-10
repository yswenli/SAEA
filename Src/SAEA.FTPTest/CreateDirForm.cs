using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class CreateDirForm : Form
    {
        public CreateDirForm()
        {
            InitializeComponent();
        }

        public string PathName { get; set; }

        private void skinButton1_Click(object sender, EventArgs e)
        {
            var pathName = skinWaterTextBox1.Text;

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
