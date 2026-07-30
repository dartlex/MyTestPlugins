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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MyTestPlugins
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : Window
    {
        public string SelectedSystemName { get; private set; }
        public UserControl1(List<string> systems)
        {
            InitializeComponent();

            SystemsListBox.ItemsSource = systems;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (SystemsListBox.SelectedItem != null)
            {
                SelectedSystemName = SystemsListBox.SelectedItem.ToString();
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Выберите имя системы");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
