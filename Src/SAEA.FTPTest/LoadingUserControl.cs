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
                return skinLabel1.Text;
            }
            set
            {
                skinLabel1.Text = value;
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
