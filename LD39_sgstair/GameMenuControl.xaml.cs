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

namespace LD39_sgstair
{
    /// <summary>
    /// Interaction logic for GameMenuControl.xaml
    /// </summary>
    public partial class GameMenuControl : UserControl
    {
        public GameMenuControl()
        {
            InitializeComponent();
        }

        private void btnStartGame_Click(object sender, RoutedEventArgs e)
        {
            GameAutomation.StartNewGame();
        }

        private void btnExitGame_Click(object sender, RoutedEventArgs e)
        {
            GameAutomation.ExitGame();
        }

        private void btnLevelEditor_Click(object sender, RoutedEventArgs e)
        {
            GameAutomation.EnterEditor();
        }
    }
}
