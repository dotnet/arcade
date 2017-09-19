using System;
using System.IO;
using System.Threading.Tasks;
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
            Application.Current.UnhandledException += OnUnhandledException;
        }

        internal static StreamWriter log;

        private void RunXunitTestsInDirectory(object sender, RoutedEventArgs e)
        {
            // Run tests for assemblies in current directory
            XunitTestRunner runner = new XunitTestRunner();
            log = Helpers.GetFileStreamWriterInLocalStorageAsync("stdout.txt").GetAwaiter().GetResult();

            Task.Run(() => runner.RunTests(App.LaunchArgs.Arguments, log));
        }

        static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.Exception as Exception;

            if (log == null)
            {
                log = Helpers.GetFileStreamWriterInLocalStorageAsync("stdout.txt").GetAwaiter().GetResult();
            }

            if (ex != null)
            {
                log.WriteLine(ex.ToString());
            }
            else
            {
                log.WriteLine("Error of unknown type thrown in application domain");
            }

            log.Dispose();
            Application.Current.Exit();
        }
    }
}
