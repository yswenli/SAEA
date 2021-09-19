using CCWin;
using SAEA.Common;
using SAEA.Common.IO;
using SAEA.FTP;
using SAEA.FTPTest.Common;
using SAEA.FTPTest.Model;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class FtpClientForm : Skin_Mac
    {
        FTPClient _client = null;

        LoadingUserControl _loadingUserControl;

        DriverHelper _driverHelper;

        public FtpClientForm()
        {
            InitializeComponent();

            _driverHelper = new DriverHelper(listView1, listView2);
        }

        #region private

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

        void RefreshLocalListView()
        {
            var filePath = textBox1.Text;

            List<ListInfo> listInfos = new List<ListInfo>();

            if (!string.IsNullOrEmpty(filePath) && filePath != "我的电脑")
            {
                try
                {
                    var dir = new DirectoryClass()
                    {
                        FullName = filePath
                    };
                    _driverHelper.GetDirectoryAndFile(dir);
                }
                catch (Exception ex)
                {
                    Log("访问本地目录：" + filePath, ex.Message);
                }
            }
            else
            {
                try
                {
                    _driverHelper.GetLocalDriver();
                }
                catch (Exception ex)
                {
                    Log("访问本地目录：" + filePath, ex.Message);
                }
            }
        }

        void Upload(string fileName, string filePath)
        {
            _loadingUserControl.Show(this);

            _loadingUserControl.Message = "正在准备上传文件...";

            Log($"正在准备上传文件{fileName}...");

            Task.Run(() =>
            {
                try
                {
                    long size = 0;

                    _client.Upload(filePath, (o, c) =>
                    {
                        if (c == 0) return;
                        size = c;
                        _loadingUserControl.Message = $"正在上传文件:{fileName},{(o * 100 / c)}%";
                    });

                    var fun = new Func<bool>(() =>
                    {
                        if (_client.FileSize(fileName) == size)
                        {
                            Log($"上传文件{fileName}成功");

                            textBox2_TextChanged(null, null);

                            return true;
                        }
                        ThreadHelper.Sleep(1000);
                        return false;
                    });

                    while (true)
                    {
                        if (fun.Invoke())
                        {
                            break;
                        }
                        if (size == 1)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log("上传文件失败，fileName:" + fileName, ex.Message);
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            });
        }

        void Download(string fileName, string filePath)
        {
            _loadingUserControl.Show(this);

            _loadingUserControl.Message = "正在准备下载文件...";

            Log($"正在准备下载文件{fileName}...");

            Task.Run(() =>
            {
                try
                {
                    _client.Download(fileName, Path.Combine(filePath, fileName), (o, c) =>
                    {
                        _loadingUserControl.Message = $"正在下载文件:{fileName}，{(o * 100 / c)}%";
                    });

                    Log($"下载文件{fileName}成功");

                    RefreshLocalListView();
                }
                catch (Exception ex)
                {
                    Log("下载文件失败，fileName:" + fileName, ex.Message);
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            });
        }


        void CreateLocalDir()
        {
            var cdf = new CreateDirForm();
            if (cdf.ShowDialog(this) == DialogResult.OK)
            {
                var pathName = cdf.PathName;

                Directory.CreateDirectory(Path.Combine(textBox1.Text, pathName));

                RefreshLocalListView();
            }
        }


        void CreateRemoteDir()
        {
            var cdf = new CreateDirForm();

            if (cdf.ShowDialog(this) == DialogResult.OK)
            {
                var pathName = cdf.PathName;

                if (_client != null && _client.Connected)
                {
                    try
                    {
                        Log($"正在创建文件夹：{pathName}");

                        _client.MakeDir(pathName);

                        textBox2_TextChanged(null, null);

                        Log($"创建文件夹：{pathName} 成功");
                    }
                    catch (Exception ex)
                    {
                        Log($"创建文件夹：{pathName} 失败", ex.Message);
                    }
                }
            }
        }
        #endregion

        #region form event

        private void FtpClientForm_Load(object sender, EventArgs e)
        {
            _loadingUserControl = new LoadingUserControl();

            _loadingUserControl.Size = this.Size;

            this.Controls.Add(_loadingUserControl);

            this.Controls.SetChildIndex(_loadingUserControl, 9999);

            RefreshLocalListView();

            _loadingUserControl.Hide(this);
        }

        private void FtpClientForm_SizeChanged(object sender, EventArgs e)
        {
            _loadingUserControl.Size = this.Size;
        }

        private void FtpClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            notifyIcon1.ShowBalloonTip(2 * 1000);
            this.Hide();
        }

        private void FtpClientForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                if (_client != null && _client.Connected)
                {
                    _client.Quit();
                }
            }
            catch { }
        }

        #endregion      

        #region context menus

        private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RefreshLocalListView();
        }

        private void refreshToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            textBox2_TextChanged(null, null);
        }

        private void parentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dir = PathHelper.GetParent(textBox1.Text);

            if (dir != null)
            {
                textBox1.Text = dir.FullName;

                RefreshLocalListView();
            }
            else
            {
                textBox1.Text = "我的电脑";

                RefreshLocalListView();
            }
        }

        private void parentToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    if (_client.ChangeToParentDir())
                    {
                        var cp = _client.CurrentDir();

                        textBox2.Invoke(new Action(() =>
                        {
                            textBox2.Text = cp;
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Log("获取ftpserver列表失败", ex.Message);
                }
            });

        }

        private void uploadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var items = listView1.SelectedItems;

            if (items == null || items.Count == 0) return;

            var dir = items[0].Tag as FileClass;

            if (dir != null)
            {
                if (_client.Connected)
                {
                    if (!dir.IsDirectory)
                    {
                        var fileName = dir.Name;

                        var filePath = Path.Combine(textBox1.Text, fileName);

                        Upload(fileName, filePath);
                    }
                    else
                    {
                        MessageBox.Show("不支持文件夹！");
                    }
                }
            }
        }

        private void downloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var filePath = textBox1.Text;

            if (string.IsNullOrEmpty(filePath) || filePath == "我的电脑")
            {
                MessageBox.Show("请在左侧列表中选择下载目录！");
                return;
            }

            var items = listView2.SelectedItems;

            if (items == null || items.Count == 0) return;

            var dir = items[0].Tag as FileClass;

            if (dir != null)
            {
                if (_client.Connected)
                {
                    if (!dir.IsDirectory)
                    {
                        var fileName = dir.Name;

                        Download(fileName, filePath);
                    }
                    else
                    {
                        MessageBox.Show("不支持文件夹！");
                    }
                }
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除此项吗？", "SAEA.FtpClient", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                var items = listView1.SelectedItems;

                if (items == null || items.Count == 0) return;

                var dir = items[0].Tag as FileClass;

                if (dir != null)
                {
                    var fileName = dir.Name;

                    var type = dir.IsDirectory;

                    if (!type)
                    {
                        try
                        {
                            File.Delete(Path.Combine(textBox1.Text, fileName));
                        }
                        catch { }
                    }
                    else
                    {
                        try
                        {
                            Directory.Delete(Path.Combine(textBox1.Text, fileName));
                        }
                        catch { }
                    }
                    RefreshLocalListView();
                }
            }
        }

        private void deleteToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("确定要删除此项吗？", "SAEA.FtpClient", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                var items = listView2.SelectedItems;

                if (items == null || items.Count == 0) return;

                var dir = items[0].Tag as FileClass;

                if (dir != null)
                {
                    var fileName = dir.Name;

                    var type = dir.IsDirectory;

                    _loadingUserControl.Message = $"正在删除{type}...";

                    _loadingUserControl.Show(this);

                    if (!dir.IsDirectory)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                Log($"正在删除文件：{fileName}");

                                _client.Delete(fileName);

                                Log($"删除文件{fileName}成功");

                                textBox2_TextChanged(null, null);
                            }
                            catch (Exception ex)
                            {
                                Log($"删除文件{fileName}失败", ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
                            }
                        });
                    }
                    else
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                Log($"正在删除文件夹：{fileName}");

                                _client.RemoveDir(fileName);

                                Log($"删除文件夹{fileName}成功");

                                textBox2_TextChanged(null, null);
                            }
                            catch (Exception ex)
                            {
                                Log($"删除文件夹{fileName}失败", ex.Message);
                            }
                            finally
                            {
                                _loadingUserControl.Hide(this);
                            }
                        });
                    }
                }
            }
        }


        private void createDirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CreateLocalDir();
        }

        private void createDirToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            CreateRemoteDir();
        }
        #endregion

        #region controls
        private void skinButton1_Click(object sender, EventArgs e)
        {
            if (skinButton1.Text == "Connect")
            {
                _loadingUserControl.Message = "正在连接到FTPServer...";

                _loadingUserControl.Show(this);

                groupBox1.Enabled = false;

                splitContainer2.Panel2.Enabled = false;

                try
                {
                    var ip = skinWaterTextBox1.Text;
                    var port = int.Parse(skinWaterTextBox2.Text);
                    var username = skinWaterTextBox3.Text;
                    var pwd = skinWaterTextBox4.Text;

                    _client = new FTPClient(ip, port, username, pwd);
                    _client.Ondisconnected += _client_Ondisconnected;

                    Task.Run(() =>
                    {
                        try
                        {
                            _client.Connect();

                            Log($"连接到FTPServer {ip}:{port}成功");

                            splitContainer2.BeginInvoke(new Action(() =>
                            {
                                groupBox1.Enabled = true;

                                skinWaterTextBox1.Enabled
                                = skinWaterTextBox2.Enabled
                                = skinWaterTextBox3.Enabled
                                = skinWaterTextBox4.Enabled = false;

                                skinButton1.Enabled = true;
                                skinButton1.Text = "Disconnect";

                                splitContainer2.Panel2.Enabled = true;
                                textBox2.Text = "/";
                                listView2.Enabled = true;
                            }));
                            textBox2_TextChanged(null, null);

                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("连接到FTPServer失败，ex:" + ex.Message);

                            Log("连接到FTPServer失败", ex.Message);

                            this.BeginInvoke(new Action(() =>
                            {
                                groupBox1.Enabled = true;
                            }));

                        }
                        finally
                        {
                            _loadingUserControl.Hide(this);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _loadingUserControl.Hide(this);
                    MessageBox.Show("连接到FTPServer失败，ex:" + ex.Message);
                    Log("连接到FTPServer失败", ex.Message);
                    groupBox1.Enabled = true;
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            }
            else
            {
                Log("正在断开FTP连接...");

                Task.Run(() =>
                {
                    try
                    {
                        _client.Dispose();
                        Log("FTP连接已断开");
                    }
                    catch (Exception ex)
                    {
                        Log("断开FTP失败", ex.Message);
                    }
                    finally
                    {
                        skinButton1.Invoke(new Action(() =>
                        {
                            skinWaterTextBox1.Enabled
                            = skinWaterTextBox2.Enabled
                            = skinWaterTextBox3.Enabled
                            = skinWaterTextBox4.Enabled
                            = true;
                            textBox2.Text = "/";
                            skinButton1.Enabled = true;
                            listView2.Enabled = false;
                            listView2.Clear();
                            skinButton1.Text = "Connect";
                            _loadingUserControl.Hide();
                        }));

                    }
                });
            }
        }

        private void _client_Ondisconnected()
        {
            Log("已断开与FTPServer的连接");

            this.Invoke(new Action(() =>
            {
                splitContainer2.Panel2.Enabled = false;
                skinWaterTextBox1.Enabled
                                = skinWaterTextBox2.Enabled
                                = skinWaterTextBox3.Enabled
                                = skinWaterTextBox4.Enabled
                                = true;
                skinButton1.Enabled = true;
                listView2.Enabled = false;
                listView2.Clear();
                skinButton1.Text = "Connect";
                logTxt.Text = "";
                _loadingUserControl.Hide();
            }));
        }


        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                RefreshLocalListView();
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            _loadingUserControl.Message = "正在获取FTPServer文件列表...";

            Log("正在获取FTPServer文件列表...");

            _loadingUserControl.Show(this);

            Task.Run(() =>
            {
                try
                {
                    var filePath = string.Empty;

                    textBox2.Invoke(new Action(() =>
                    {
                        filePath = textBox2.Text;
                    }));


                    var list = _client.Dir(filePath, FTP.Model.DirType.MLSD);

                    Log("已获取FTPServer文件列表");

                    if (list != null && list.Any())
                    {
                        List<ListInfo> listInfos = new List<ListInfo>();

                        foreach (var item in list)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(item))
                                {
                                    var arr = item.Split(";", StringSplitOptions.RemoveEmptyEntries);

                                    if (arr.Length >= 3)
                                    {
                                        var fileName = arr[2].Trim();

                                        var type = (arr[0] == "type=dir" ? "文件夹" : "文件");

                                        var size = 0L;

                                        if (type == "文件")
                                        {
                                            fileName = arr[3].Trim();

                                            var sizeStr = arr[2].Substring(arr[2].IndexOf("=") + 1);

                                            long.TryParse(sizeStr, out size);
                                        }

                                        listInfos.Add(new ListInfo()
                                        {
                                            FileName = fileName,
                                            Type = type,
                                            Size = size
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log("获取FTPServer文件列表出现异常", ex.Message);
                            }
                        }

                        listInfos = listInfos.OrderBy(b => b.FileName).ToList();

                        listView2.BeginInvoke(new Action(() =>
                        {
                            listView2.Clear();
                            _driverHelper.GetRemoteDirectoryAndFile(listInfos);
                        }));
                    }
                    else
                    {
                        listView2.BeginInvoke(new Action(() =>
                        {
                            listView2.Clear();
                        }));
                    }
                }
                catch (Exception ex)
                {
                    Log("初始化ftpserver列表失败", ex.Message);
                }
                finally
                {
                    _loadingUserControl.Hide(this);
                }
            });
        }

        private void listView1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listView1.SelectedItems.Count <= 0)
            {
                return;
            }
            string typeName = listView1.SelectedItems[0].SubItems[1].Text;
            if (typeName == "文件夹" || typeName == "本地磁盘")
            {
                DirectoryClass dir = listView1.SelectedItems[0].Tag as DirectoryClass;
                listView1.Items.Clear();
                _driverHelper.GetDirectoryAndFile(dir);
                textBox1.Text = dir.FullName;
            }
            else
            {
                FileClass file = listView1.SelectedItems[0].Tag as FileClass;
                Process.Start(file.FullName);
            }
        }
        private void listView2_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (listView2.SelectedItems.Count <= 0)
            {
                return;
            }
            string typeName = listView2.SelectedItems[0].SubItems[1].Text;
            if (typeName == "文件夹" || typeName == "本地磁盘")
            {
                DirectoryClass dir = listView2.SelectedItems[0].Tag as DirectoryClass;

                Task.Run(() =>
                {
                    try
                    {
                        if (_client.ChangeDir(dir.Name))
                        {
                            var cp = _client.CurrentDir();

                            textBox2.Invoke(new Action(() =>
                            {
                                textBox2.Text = cp;
                            }));
                        }
                        else
                        {
                            textBox2_TextChanged(null, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("获取ftpserver列表失败", ex.Message);
                    }
                });
            }
            else
            {
                Task.Run(() =>
                {
                    try
                    {
                        if (_client.ChangeToParentDir())
                        {
                            var cp = _client.CurrentDir();

                            textBox2.Invoke(new Action(() =>
                            {
                                textBox2.Text = cp;
                            }));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("获取ftpserver列表失败", ex.Message);
                    }
                });
            }
        }

        #endregion

        #region notify
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
        }

        private void showUIToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Show();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                _client?.Dispose();
            }
            catch { }
            try
            {
                notifyIcon1.Dispose();
            }
            catch { }
            try
            {
                this.Close();
            }
            catch { }
        }


        #endregion


    }
}
