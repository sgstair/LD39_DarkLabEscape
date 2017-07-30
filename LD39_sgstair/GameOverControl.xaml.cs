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
    /// Interaction logic for GameOverControl.xaml
    /// </summary>
    public partial class GameOverControl : UserControl
    {
        public GameOverControl()
        {
            InitializeComponent();
        }


        public void UpdateText()
        {
            GameState state = GameAutomation.State;

            if(state.WonGame)
            {
                Exposition.Content = "The last door opened to reveal the sun setting outside the dark facility\nThe scientist breathes a deep sign of relief.";
            }
            else
            {
                Exposition.Content = "As the facility's robotic announcer distorts, the lighting fades to pitch black\nThe scientist huddles into a corner, all alone in the darkness.";
            }

            // Generate stats
            List<string> statsList = new List<string>();
            statsList.Add("Stats:");

            if (state.RemainingPower < 0) state.RemainingPower = 0;
            double remainingPercent = state.RemainingPower / state.StartingPower;
            statsList.Add($"Power Remaining: {state.RemainingPower:n1} seconds ({remainingPercent * 100:n2}%)");

            statsList.Add($"Energy Beam on time: {state.LaserOnTime:n1} seconds");

            // Future: wall damage + property damage


            Stats.Content = string.Join("\n", statsList);
        }

        private void btnReturnMenu_Click(object sender, RoutedEventArgs e)
        {
            GameAutomation.EnterMenu();
        }
    }
}
