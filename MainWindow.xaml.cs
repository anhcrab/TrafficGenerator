using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Terus_Traffic
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly List<ClientWindow> ClientWindows = new List<ClientWindow>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void AddBtn_Click(object sender, RoutedEventArgs e)
        {
            var input = Input.Text;
            var length = 0;
            if (input != null && input != "")
            {
                int.TryParse(input, out length);
            }
            for (int i = 0; i < length; i++)
            {
                var clientWindow = new ClientWindow();
                clientWindow.Show();
                ClientWindows.Add(clientWindow);
            }
        }

        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            ClientWindows.ForEach(client => client.Close());
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Input_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void UploadFile_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

            bool? response = openFileDialog.ShowDialog();

            if (response == true)
            {
                string filePath = openFileDialog.FileName;
                MessageBox.Show(filePath);

            }
        }
    }
}
