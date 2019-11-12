using SAEA.FTPTest.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class CreateUserForm : Form
    {
        public CreateUserForm()
        {
            InitializeComponent();
        }


        public FtpServerUser FtpServerUser
        {
            get; set;
        }


        private void skinButton1_Click(object sender, EventArgs e)
        {
            var userName = skinWaterTextBox1.Text;

            if (string.IsNullOrWhiteSpace(userName))
            {
                MessageBox.Show("名称不能为空！");
                return;
            }

            var password = skinWaterTextBox2.Text;

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("password不能为空！");
                return;
            }

            var dataPort = 39654;

            var dataPortStr = skinWaterTextBox3.Text;

            if (string.IsNullOrWhiteSpace(dataPortStr))
            {
                MessageBox.Show("dataPort不能为空！");
                return;
            }

            if (!int.TryParse(dataPortStr, out dataPort))
            {
                MessageBox.Show("dataPort不是数字！");
                return;
            }

            if (dataPort < 20 || dataPort > 65536)
            {
                MessageBox.Show("dataPort必须在20-63536之间！");
                return;
            }

            var root = skinWaterTextBox4.Text;

            if (string.IsNullOrWhiteSpace(root))
            {
                MessageBox.Show("名称不能为空！");
                return;
            }


            this.FtpServerUser = new FtpServerUser()
            {
                UserName = userName,
                Password = password,
                DataPort = dataPort,
                Root = root
            };

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
