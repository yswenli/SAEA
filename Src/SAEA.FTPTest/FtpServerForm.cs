using CCWin;
using SAEA.Common;
using SAEA.FTP;
using SAEA.FTP.Core;
using SAEA.FTP.Model;
using SAEA.FTPTest.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class FtpServerForm : Skin_Mac
    {
        ServerConfig _serverConfig;

        List<FtpServerUser> _ftpServerUsers;

        FTPServer _ftpServer = null;

        public FtpServerForm()
        {
            InitializeComponent();
        }


        void Init()
        {
            _serverConfig = FTPServerConfigManager.Get();

            skinWaterTextBox1.Text = _serverConfig.IP.ToString();
            skinWaterTextBox2.Text = _serverConfig.Port.ToString();

            if (_serverConfig.Users == null)
            {
                _serverConfig.Users = new ConcurrentDictionary<string, FTPUser>();
            }

            if (!_serverConfig.Users.Any())
            {
                _serverConfig.Users.TryAdd("anonymous", new FTPUser("anonymous", "yswenli@outlook.com", "c:\\"));
                FTPServerConfigManager.Save();
            }

            _ftpServerUsers = SerializeHelper.Deserialize<List<FtpServerUser>>(SerializeHelper.Serialize(_serverConfig.Users.Values));

            dataGridView1.DataSource = null;

            dataGridView1.DataSource = _ftpServerUsers;

            dataGridView1.AllowUserToAddRows = true;

        }

        private void FtpServerForm_Load(object sender, EventArgs e)
        {
            Init();
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void skinButton3_Click(object sender, EventArgs e)
        {
            var cf = new CreateUserForm();

            if (cf.ShowDialog(this) == DialogResult.OK)
            {
                var user = cf.FtpServerUser;

                if (FTPServerConfigManager.GetUser(user.UserName) != null)
                {
                    MessageBox.Show("当前用户已存在！");
                    return;
                }

                _ftpServerUsers.Add(user);
                FTPServerConfigManager.SetUser(user.UserName, user.Password, user.Root);
                FTPServerConfigManager.Save();
                Init();
            }
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void skinButton2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确认要删除用户吗？", "SAEA.FTP Test", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {

                List<string> userNames = new List<string>();

                var rows = dataGridView1.SelectedRows;

                if (rows == null || rows.Count == 0)
                {
                    var cells = dataGridView1.SelectedCells;

                    if (cells == null && cells.Count == 0)
                    {
                        MessageBox.Show("请选择要删除的用户");
                        return;
                    }
                    else
                    {
                        foreach (DataGridViewTextBoxCell item in cells)
                        {
                            userNames.Add(dataGridView1.Rows[item.RowIndex].Cells[0].Value.ToString());
                        }
                    }
                }

                foreach (DataGridViewRow item in rows)
                {
                    userNames.Add(item.Cells[0].Value.ToString());
                }

                if (!userNames.Any())
                {
                    MessageBox.Show("请选择要删除的用户");
                    return;
                }
                else
                {
                    foreach (var userName in userNames)
                    {
                        if (!string.IsNullOrEmpty(userName))
                        {
                            var user = _ftpServerUsers.FirstOrDefault(b => b.UserName == userName);

                            if (user != null)
                            {
                                _ftpServerUsers.Remove(user);

                                FTPServerConfigManager.DelUser(userName);
                            }
                        }
                    }

                    FTPServerConfigManager.Save();
                    Init();
                }
            }
        }

        /// <summary>
        /// 修改用户
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex >= 0 && e.RowIndex >= 0)
            {
                var row = dataGridView1.Rows[e.RowIndex];

                var userName = row.Cells[0].Value.ToString();

                var oUser = _ftpServerUsers.FirstOrDefault(b => b.UserName == userName);

                if (oUser == null) return;

                var cf = new CreateUserForm(oUser);

                if (cf.ShowDialog(this) == DialogResult.OK)
                {
                    var user = cf.FtpServerUser;
                    _ftpServerUsers.Remove(oUser);
                    _ftpServerUsers.Add(user);
                    FTPServerConfigManager.SetUser(user.UserName, user.Password, user.Root);
                    FTPServerConfigManager.Save();
                    Init();
                }
            }
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

        void Log(string operationName, string msg = "")
        {
            var action = new Action(() =>
            {
                logTxt.Text = $"{DateTimeHelper.Now.ToString("HH:mm:ss.fff")}\t{operationName} \t{msg}{Environment.NewLine}{logTxt.Text}";
                logTxt.Select(0, 1);
                logTxt.ScrollToCaret();
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

        private void skinButton1_Click(object sender, EventArgs e)
        {
            if (skinButton1.Text == "Start")
            {
                var ip = skinWaterTextBox1.Text;

                var portStr = skinWaterTextBox2.Text;

                if (!IPHelper.IsIP(ip))
                {
                    MessageBox.Show("输入的ip有误");
                    Log("输入的ip有误");
                    return;
                }
                if (!IPHelper.IsPort(portStr, out ushort port))
                {
                    MessageBox.Show("输入的ip有误");
                    Log("输入的ip有误");
                    return;
                }

                _serverConfig.IP = ip;
                _serverConfig.Port = port;
                FTPServerConfigManager.Save();

                Log("FTPServer正在启动中...");

                skinButton1.Text = "Stop";

                if (_ftpServer == null)
                {
                    _ftpServer = new FTPServer(_serverConfig.IP, _serverConfig.Port, _serverConfig.BufferSize);
                    _ftpServer.OnLog += _ftpServer_OnLog;
                }

                _ftpServer.Start();

                Log("FTPServer已启动");
            }
            else
            {
                _ftpServer.Stop();
                this.Close();
            }
        }

        private void _ftpServer_OnLog(string msg, Exception ex)
        {
            Log(msg, ex?.Message);
        }
    }
}
