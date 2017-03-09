using System;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace XUnit.Runner.Uap
{
    public sealed partial class MainPage : Page
    {

        public MainPage()
        {
            this.InitializeComponent();
            log = new StringBuilder();
        }

        internal static StringBuilder log;

        public async void UpdateTextBox(string text)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                Windows.UI.Core.CoreDispatcherPriority.Normal,
            () =>
            {
                log.AppendLine(text);
                outputTextBox.Text += $"{text}\r\n";
            });
        }

        private void RunXunitTestsInDirectory(object sender, RoutedEventArgs e)
        {
            // Run tests for assemblies in current directory
            XunitTestRunner runner = new XunitTestRunner();
            UpdateTextBox("Starting...");

            Task.Run(() => runner.RunTests(App.LaunchArgs.Arguments, log, UpdateTextBox));
        }
    }
}
