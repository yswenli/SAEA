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

        public CreateUserForm(FtpServerUser ftpServerUser) : this()
        {
            skinWaterTextBox1.Text = ftpServerUser.UserName;
            skinWaterTextBox2.Text = ftpServerUser.Password;
            skinWaterTextBox4.Text = ftpServerUser.Root;
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
