using CCWin;
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
    /// <summary>
    /// 演示程序入口窗体，分别打开 FTP 服务端与客户端窗口
    /// </summary>
    public partial class MainForm : Skin_Mac
    {
        FtpServerForm _serverForm = null;
        FtpClientForm _clientForm = null;

        /// <summary>
        /// 初始化入口窗体
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
        }

        private void skinButton1_Click(object sender, EventArgs e)
        {
            if (_serverForm == null || _serverForm.IsDisposed)
            {
                _serverForm = new FtpServerForm();
            }
            _serverForm.Show();
        }

        private void skinButton2_Click(object sender, EventArgs e)
        {
            if (_clientForm == null || _clientForm.IsDisposed)
            {
                _clientForm = new FtpClientForm();
            }
            _clientForm.Show();
        }
    }
}
