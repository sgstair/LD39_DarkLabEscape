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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            GameAutomation.PrepareGame(this);

            Closing += MainWindow_Closing;
            RestoreWindowPosition();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Save window X/Y location for future use.
            SaveWindowPosition();
        }

        void SaveWindowPosition()
        {
            Properties.Settings.Default.WindowX = Left.ToString();
            Properties.Settings.Default.WindowY = Top.ToString();

            Properties.Settings.Default.Save();
        }
        void RestoreWindowPosition()
        {
            int x, y;
            if(int.TryParse(Properties.Settings.Default.WindowX, out x))
            {
                if (int.TryParse(Properties.Settings.Default.WindowY, out y))
                {
                    Left = x;
                    Top = y;
                }
            }
        }

        public void SetContent(FrameworkElement e)
        {
            grid.Children.Clear();
            e.HorizontalAlignment = HorizontalAlignment.Left;
            e.VerticalAlignment = VerticalAlignment.Top;
            e.Margin = new Thickness(0);
            grid.Children.Add(e);
        }
    }

    class GameAutomation
    {
        static MainWindow parentWindow;
        static GameMenuControl menuControl = new GameMenuControl();
        static GameLevelControl levelControl = new GameLevelControl();
        static LevelEditorControl editorControl = new LevelEditorControl();
        public static void PrepareGame(MainWindow bindMainWindow)
        {
            parentWindow = bindMainWindow;
            // Future: maybe show pre-menu slides?

            // For now, enter level directly for test.
            // EnterMenu();

            //EnterLevel(0);
            EnterEditor();
        }

        public static void EnterLevel(int level)
        {
            levelControl.SetLevel(GetLevel(level));
            parentWindow.SetContent(levelControl);
        }
        public static void EnterMenu()
        {
            parentWindow.SetContent(menuControl);
        }

        public static void EnterEditor()
        {
            parentWindow.SetContent(editorControl);
        }

        public static void EnterTestLevel(Level lvl)
        {
            levelControl.SetTestLevel(lvl);
            parentWindow.SetContent(levelControl);
        }


        public static void ExitGame()
        {
            parentWindow.Close();
        }


        public static Level GetLevel(int index)
        {
            Level l =  LevelGenerator.GenerateLevel(Guid.NewGuid());
            l.LevelIndex = index;
            return l;
        }
    }
}
