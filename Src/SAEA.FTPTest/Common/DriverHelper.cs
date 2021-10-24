/****************************************************************************
*项目名称：SAEA.FTPTest.Common
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTPTest.Common
*类 名 称：DriverHelper
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/18 15:39:58
*描述：
*=====================================================================
*修改时间：2019/11/18 15:39:58
*修 改 人： yswenli
*版本号： v7.0.0.1
*描    述：
*****************************************************************************/
using SAEA.FTPTest.Model;
using SAEA.FTPTest.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SAEA.FTPTest.Common
{
    class DriverHelper
    {
        ListView _localListView;

        ListView _remoteListView;

        SystemIconsImageList _sysIcons;

        CustomerViewList _customerChoose;

        public CustomerViewList CustomerChoose
        {
            get
            {
                return _customerChoose;
            }
            set
            {

                _customerChoose = value;

                switch (_customerChoose.ToString())
                {
                    case "平铺":
                        _remoteListView.View = _localListView.View = View.Tile;
                        _customerChoose = CustomerViewList.平铺;
                        break;
                    case "大图标":
                        _remoteListView.View = _localListView.View = View.LargeIcon;
                        _customerChoose = CustomerViewList.大图标;
                        break;
                    case "小图标":
                        _remoteListView.View = _localListView.View = View.SmallIcon;
                        _customerChoose = CustomerViewList.小图标;
                        break;
                    case "列表":
                        _remoteListView.View = _localListView.View = View.List;
                        _customerChoose = CustomerViewList.列表;
                        break;
                    case "详细信息":
                        _remoteListView.View = _localListView.View = View.Details;
                        _customerChoose = CustomerViewList.详细信息;
                        break;
                };
            }
        }

        public DriverHelper(ListView localListView, ListView remoteListView)
        {
            _localListView = localListView;

            _remoteListView = remoteListView;

            _sysIcons = new SystemIconsImageList();

            _sysIcons.SmallImageList.Images.Add("MyDriver", Resources.MyDriver);
            _sysIcons.LargeImageList.Images.Add("MyDriver", Resources.MyDriver);

            _sysIcons.SmallImageList.Images.Add("FileFolder", Resources.FileFolder);
            _sysIcons.LargeImageList.Images.Add("FileFolder", Resources.FileFolder);

            _localListView.SmallImageList = _sysIcons.SmallImageList;
            _localListView.LargeImageList = _sysIcons.LargeImageList;

            _remoteListView.SmallImageList = _sysIcons.SmallImageList;
            _remoteListView.LargeImageList = _sysIcons.LargeImageList;

            this.CustomerChoose = CustomerViewList.详细信息;
        }




        /// <summary>
        /// 得到磁盘的资料  
        /// </summary>
        public void GetLocalDriver()
        {
            _localListView.Clear();

            _localListView.Columns.Add("columnsName", "名称", 200);
            _localListView.Columns.Add("columnsType", "类型", 80);
            _localListView.Columns.Add("columnsMax", "总大小", 100);
            _localListView.Columns.Add("columnsFreeSpace", "可用空间", 80);

            FixDriverColumnHeader();

            foreach (DriveInfo driver in DriveInfo.GetDrives())
            {
                if (driver.DriveType == DriveType.Fixed)  //如果是硬盘
                {
                    DirectoryClass driverBase = new DirectoryClass();
                    driverBase.Name = driver.Name;
                    driverBase.FullName = driver.Name;
                    driverBase.IsDirectory = true;
                    driverBase.FreePrice = Math.Round((driver.TotalFreeSpace / 1024.0 / 1024.0 / 1024.0), 1);//可用空间
                    driverBase.MaxLength = Math.Round((driver.TotalSize / 1024.0 / 1024.0 / 1024.0), 1); //总大小

                    ListViewItem item = new ListViewItem();
                    item.Text = string.Format("{0}({1})", driver.VolumeLabel, driver.Name);
                    item.Tag = driverBase;
                    item.ImageIndex = _sysIcons.SmallImageList.Images.IndexOfKey("MyDriver");

                    //子项
                    item.SubItems.Add("本地磁盘");  //类型

                    item.SubItems.Add(driverBase.MaxLength + "GB");

                    //可用空间                 
                    item.SubItems.Add(driverBase.FreePrice + "GB");
                    _localListView.Items.Add(item);
                }


            }
        }

        /// <summary>
        /// 设定标题头，只针对固定的硬盘
        /// </summary>
        private void FixDriverColumnHeader()
        {
            //重新加项目的列
            if (CustomerChoose == CustomerViewList.平铺)
            {
                foreach (ListViewItem lst in _localListView.Items)
                {
                    _localListView.Columns[0].Width = 180;
                    //lst.SubItems[1].Text = "";
                    lst.SubItems[2].Text = "";
                    lst.SubItems[3].Text = "";
                }

            }
            else
            {
                foreach (ListViewItem lst in _localListView.Items)
                {
                    DirectoryClass dc = lst.Tag as DirectoryClass;

                    lst.SubItems[1].Text = "本地磁盘";
                    lst.SubItems[2].Text = dc.MaxLength + "GB";  //修改日期
                    lst.SubItems[3].Text = dc.FreePrice + "GB";  //
                }
            }

        }

        /// <summary>
        /// 根据节点信息，得到文件夹和文件，把显示到ListView列表中
        /// </summary>
        /// <param name="info"></param>
        public void GetDirectoryAndFile(DirectoryClass info)
        {
            var action = new Action(() =>
            {
                _localListView.Clear();

                DirectoryInfo dirs = new DirectoryInfo(info.FullName);
                if (dirs.ToString() == @"C:\Documents and Settings\Administrator\桌面") return;

                //循环添加文件夹

                foreach (DirectoryInfo dir in dirs.GetDirectories())
                {
                    if (dir.Name == "$RECYCLE.BIN" || dir.Name == "System Volume Information") continue;

                    #region 如果文件是只读的就不要读出来，不然会报错
                    string[] fileAttrites = File.GetAttributes(dir.FullName).ToString().Split(',');
                    //如果属性大于0的就说明有问题,只要属性里面有hidden就不要显示出来

                    bool isHideen = false; //是不是隐藏文件

                    foreach (string cAttrites in fileAttrites)
                    {
                        if (cAttrites.Equals("hidden", StringComparison.InvariantCultureIgnoreCase))
                        {
                            isHideen = true;
                            break;
                        }
                    }
                    #endregion

                    #region 判断isHideen=false，这样就可以添加资料到ListView中去

                    if (!isHideen)  //文件夹
                    {
                        DirectoryClass dc = new DirectoryClass();
                        dc.Name = dir.Name;
                        dc.FullName = dir.FullName;
                        dc.IsDirectory = true;
                        dc.IsFixDriver = false;
                        dc.CreateTime1 = dir.LastWriteTime.ToString("yyyy/MM/dd HH:mm");

                        ListViewItem item = new ListViewItem();
                        item.Text = dc.Name;
                        item.Tag = dc;
                        item.ImageIndex = _sysIcons.SmallImageList.Images.IndexOfKey("FileFolder");

                        item.SubItems.Add("文件夹");
                        item.SubItems.Add(dc.CreateTime1);
                        item.SubItems.Add("");

                        _localListView.Items.Add(item);
                    }

                    #endregion

                }

                //循环添加文件
                foreach (FileInfo file in dirs.GetFiles())
                {
                    #region 如果文件是只读的就不要读出来，不然会报错
                    string[] fileAttrites = File.GetAttributes(file.FullName).ToString().Split(',');
                    //如果属性大于0的就说明有问题,只要属性里面有hidden就不要显示出来

                    bool isHideen = false; //是不是隐藏文件

                    foreach (string cAttrites in fileAttrites)
                    {
                        if (cAttrites.Equals("hidden", StringComparison.InvariantCultureIgnoreCase) || cAttrites.Equals("readonly", StringComparison.InvariantCultureIgnoreCase))
                        {
                            isHideen = true;
                            break;
                        }
                    }
                    #endregion

                    #region 如果isHideen==false就显示文件

                    if (!isHideen)
                    {
                        FileClass newFile = new FileClass();
                        newFile.Name = file.Name;
                        newFile.FullName = file.FullName;
                        newFile.FileExtends = file.Extension;  //扩展名
                        newFile.CreateTime1 = file.LastWriteTime.ToString("yyyy/MM/dd HH:mm");


                        newFile.FileMax = Math.Round((file.Length / 1024.0 / 1024.0), 2);

                        newFile.IsDirectory = false;

                        //文件的扩展名称叫什么
                        string desc;

                        IconHelper.GetExtsDescription(newFile.FileExtends, out desc);

                        newFile.FileExtendsName = desc;


                        ListViewItem item = new ListViewItem();
                        item.Text = newFile.Name;
                        item.Tag = newFile;
                        item.ImageIndex = _sysIcons.GetIconIndex(newFile.FullName, false);

                        //子项
                        item.SubItems.Add(newFile.FileExtendsName);
                        item.SubItems.Add(newFile.CreateTime1);

                        double len = newFile.FileMax;
                        if (len >= 1000)
                        {
                            len = Math.Round((len / 1024.0), 2);

                            item.SubItems.Add(len + "GB");
                        }
                        else
                        {
                            item.SubItems.Add(newFile.FileMax + "MB");
                        }

                        _localListView.Items.Add(item);
                    }
                    #endregion
                }

                FillDirectoryHeader();
            });

            if (_localListView.InvokeRequired)
            {
                _localListView.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }


        /// <summary>
        /// 填充文件夹的标题头
        /// </summary>
        private void FillDirectoryHeader()
        {
            if (CustomerChoose == CustomerViewList.平铺)
            {
                foreach (ListViewItem lst in _localListView.Items)
                {
                    _localListView.Columns[0].Width = 200;
                    //lst.SubItems[1].Text = "";
                    lst.SubItems[2].Text = "";
                    lst.SubItems[3].Text = "";
                }

            }
            else
            {
                _localListView.Columns.Add("columnsName", "名称", 200);
                _localListView.Columns.Add("columnsType", "类型", 200);
                _localListView.Columns.Add("columnsMax", "修改日期", 150);
                _localListView.Columns.Add("columnsFreeSpace", "大小", 80);

                foreach (ListViewItem lst in _localListView.Items)
                {
                    if (lst.SubItems[1].Text == "文件夹")
                    {
                        DirectoryClass dc = lst.Tag as DirectoryClass;
                        lst.SubItems[1].Text = "文件夹";
                        lst.SubItems[2].Text = dc.CreateTime1;  //修改日期
                    }
                    else
                    {
                        FileClass dc = lst.Tag as FileClass;
                        lst.SubItems[1].Text = dc.FileExtendsName;  //文件类型
                        lst.SubItems[2].Text = dc.CreateTime1;  //修改日期
                        lst.SubItems[3].Text = dc.FileMax + "MB";  //大小
                    }
                }
            }
        }



        public void GetRemoteDirectoryAndFile(List<ListInfo> listInfos)
        {
            var action = new Action(() =>
            {
                _remoteListView.Clear();

                //循环添加文件夹

                foreach (var dir in listInfos)
                {
                    ListViewItem item = new ListViewItem();

                    item.Text = dir.FileName;

                    item.SubItems.Add(dir.Type);

                    if (dir.Type == "文件夹")
                    {
                        item.ImageIndex = _sysIcons.SmallImageList.Images.IndexOfKey("FileFolder");

                        item.Tag = new DirectoryClass()
                        {
                            Name = dir.FileName
                        };
                    }
                    else
                    {
                        item.ImageIndex = _sysIcons.GetIconIndex(dir.FileName, false);

                        var ext = Path.GetExtension(dir.FileName);

                        string desc = string.Empty;

                        IconHelper.GetExtsDescription(ext, out desc);

                        item.Tag = new FileClass()
                        {
                            Name = dir.FileName,
                            FileExtendsName = desc,
                            FileMax = dir.Size / (1024 * 1024)
                        };

                    }
                    item.SubItems.Add("");
                    item.SubItems.Add(dir.Size.ToString());

                    _remoteListView.Items.Add(item);

                }

                FillRemoteDirectoryHeader();
            });
            if (_remoteListView.InvokeRequired)
            {
                _remoteListView.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }

        /// <summary>
        /// 填充文件夹的标题头
        /// </summary>
        private void FillRemoteDirectoryHeader()
        {
            if (CustomerChoose == CustomerViewList.平铺)
            {
                foreach (ListViewItem lst in _remoteListView.Items)
                {
                    _remoteListView.Columns[0].Width = 200;
                    //lst.SubItems[1].Text = "";
                    lst.SubItems[2].Text = "";
                    lst.SubItems[3].Text = "";
                }

            }
            else
            {
                _remoteListView.Columns.Add("columnsName", "名称", 200);
                _remoteListView.Columns.Add("columnsType", "类型", 200);
                _remoteListView.Columns.Add("columnsMax", "修改日期", 150);
                _remoteListView.Columns.Add("columnsFreeSpace", "大小", 80);

                foreach (ListViewItem lst in _remoteListView.Items)
                {
                    if (lst.SubItems[1].Text == "文件夹")
                    {
                        DirectoryClass dc = lst.Tag as DirectoryClass;
                        lst.SubItems[1].Text = "文件夹";
                        lst.SubItems[2].Text = dc.CreateTime1;  //修改日期
                    }
                    else
                    {
                        FileClass dc = lst.Tag as FileClass;
                        lst.SubItems[1].Text = dc.FileExtendsName;  //文件类型
                        lst.SubItems[2].Text = dc.CreateTime1;  //修改日期
                        lst.SubItems[3].Text = dc.FileMax + "MB";  //大小
                    }
                }
            }
        }
    }
}
