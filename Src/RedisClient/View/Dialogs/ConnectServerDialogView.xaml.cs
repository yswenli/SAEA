using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RedisClient
{
    /// <summary>
    /// ConnectServerDialogView.xaml 的交互逻辑
    /// </summary>
    public partial class ConnectServerDialogView : Window
    {
        public ConnectServerDialogView()
        {
            InitializeComponent();
            this.DataContextChanged += ConnectServerDialogView_DataContextChanged;
            this.passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            (this.DataContext as ConnectServerDialogViewModel).Password = this.passwordBox.Password;
        }

        private void ConnectServerDialogView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.passwordBox.Password=  (this.DataContext as ConnectServerDialogViewModel).Password ;
           
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

     
    }
}
