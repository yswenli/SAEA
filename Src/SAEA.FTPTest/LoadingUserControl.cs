using System;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    /// <summary>
    /// 覆盖在窗体上的加载遮罩控件，显示进度提示文本
    /// </summary>
    public partial class LoadingUserControl : UserControl
    {
        /// <summary>
        /// 初始化加载遮罩控件
        /// </summary>
        public LoadingUserControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 遮罩上显示的提示文本，get/set 均自动切回 UI 线程执行
        /// </summary>
        public string Message
        {
            get
            {
                string msg = string.Empty;

                var action = new Action(() =>
                {
                    msg = skinLabel1.Text;
                });
                if (this.InvokeRequired)
                {
                    this.Invoke(action);
                }
                else
                {
                    action.Invoke();
                }
                return msg;
            }
            set
            {
                var action = new Action(() =>
                {
                    skinLabel1.Text = value;
                });
                if (this.InvokeRequired)
                {
                    this.Invoke(action);
                }
                else
                {
                    action.Invoke();
                }
            }
        }

        /// <summary>
        /// 显示遮罩并置于最前，必要时通过父窗体调度到 UI 线程
        /// </summary>
        /// <param name="parent">提供 Invoke 的父窗体</param>
        public void Show(Form parent)
        {
            var action = new Action(() =>
            {
                this.Visible = true;
                this.BringToFront();
            });

            if (parent.InvokeRequired)
            {
                parent.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }

        /// <summary>
        /// 隐藏遮罩，必要时通过父窗体调度到 UI 线程
        /// </summary>
        /// <param name="parent">提供 Invoke 的父窗体</param>
        public void Hide(Form parent)
        {
            var action = new Action(() =>
            {
                this.Visible = false;
            });

            if (parent.InvokeRequired)
            {
                parent.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
        }
    }
}
