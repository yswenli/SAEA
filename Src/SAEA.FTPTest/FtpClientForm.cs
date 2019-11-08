using CCWin;
using SAEA.FTP;
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
    public partial class FtpClientForm : Skin_Mac
    {
        FTPClient _client = null;

        public FtpClientForm()
        {
            InitializeComponent();
        }

        private void skinButton1_Click(object sender, EventArgs e)
        {
            groupBox1.Enabled = false;
            try
            {
                var ip = skinWaterTextBox1.Text;
                var port = int.Parse(skinWaterTextBox2.Text);
                var username = skinWaterTextBox3.Text;
                var pwd = skinWaterTextBox4.Text;

                _client = new FTPClient(ip, port, username, pwd);

                Task.Run(() =>
                {
                    try
                    {
                        _client.Connect();
                        Log("连接到FTP成功");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("连接到FTP失败，ex:" + ex.Message);
                        Log("连接到FTP失败", ex.Message);

                        this.BeginInvoke(new Action(() =>
                        {
                            groupBox1.Enabled = true;
                        }));
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("连接到FTP失败，ex:" + ex.Message);
                Log("连接到FTP失败", ex.Message);
                groupBox1.Enabled = true;
            }
        }


        public void Log(string operationName, string msg = "")
        {
            var action = new Action(() =>
            {
                logTxt.Text = $"{DateTime.Now.ToString("HH:mm:ss.fff")}\t{operationName} \t{msg}{Environment.NewLine}{logTxt.Text}";
            });

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(action));
            }
            else
            {
                action.Invoke();
            }
        }
    }
}
