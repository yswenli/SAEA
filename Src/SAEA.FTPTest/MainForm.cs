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
    public partial class MainForm : Skin_Mac
    {
        FtpServerForm _serverForm = null;
        FtpClientForm _clientForm = null;

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
