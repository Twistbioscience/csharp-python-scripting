using System.Windows;
using System.Windows.Input;
using Twist.PyCon;

namespace PyCon
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _context;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;

            _context = new MainViewModel();
            this.DataContext = _context;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InputBlock.KeyDown += InputBlock_KeyDown;
            InputBlock.Focus();
        }

        async void InputBlock_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                InputBlock.IsEnabled = false;
                try
                {
                    await _context.ConsoleVM.RunCommand();
                }
                finally
                {
                    InputBlock.IsEnabled = true;
                    InputBlock.Focus();
                    Scroller.ScrollToBottom();
                }
            }
        }
    }
}
