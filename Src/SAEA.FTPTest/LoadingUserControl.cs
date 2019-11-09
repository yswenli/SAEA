using System;
using System.Windows.Forms;

namespace SAEA.FTPTest
{
    public partial class LoadingUserControl : UserControl
    {
        public LoadingUserControl()
        {
            InitializeComponent();
        }

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
