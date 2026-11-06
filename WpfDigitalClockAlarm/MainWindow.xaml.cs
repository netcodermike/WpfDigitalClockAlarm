using System.ComponentModel;
using System.Windows;
using WpfDigitalClockAlarm.ViewModels;

namespace WpfDigitalClockAlarm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
            base.OnClosing(e);
        }
    }
}
