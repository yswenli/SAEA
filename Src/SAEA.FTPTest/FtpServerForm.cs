using CCWin;
using SAEA.Common;
using SAEA.FTP;
using SAEA.FTP.Core;
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
    public partial class FtpServerForm : Skin_Mac
    {
        ServerConfig _serverConfig;

        List<FtpServerUser> _ftpServerUsers;

        public FtpServerForm()
        {
            InitializeComponent();
        }


        void Init()
        {
            _serverConfig = FTPServerConfigManager.Get();

            skinWaterTextBox1.Text = _serverConfig.Port.ToString();

            if (_serverConfig.Users != null && _serverConfig.Users.Any())
            {
                _ftpServerUsers = SerializeHelper.Deserialize<List<FtpServerUser>>(SerializeHelper.Serialize(_serverConfig.Users));

                dataGridView1.DataSource = null;

                dataGridView1.DataSource = _ftpServerUsers;
            }

        }

        private void FtpServerForm_Load(object sender, EventArgs e)
        {

        }



        private void skinButton1_Click(object sender, EventArgs e)
        {

        }

        private void FtpServerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("确定要退出吗？", "SAEA.FTPServer", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                e.Cancel = false;
            }
            else
            {
                e.Cancel = true;
            }
        }
    }
}
