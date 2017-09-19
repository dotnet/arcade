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

        public void UpdateLog(string text)
        {
            log.AppendLine(text);
        }

        private void RunXunitTestsInDirectory(object sender, RoutedEventArgs e)
        {
            // Run tests for assemblies in current directory
            XunitTestRunner runner = new XunitTestRunner();

            Task.Run(() => runner.RunTests(App.LaunchArgs.Arguments, log, UpdateLog));
        }
    }
}
