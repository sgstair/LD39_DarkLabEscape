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
    /// Interaction logic for LevelEditorControl.xaml
    /// </summary>
    public partial class LevelEditorControl : UserControl
    {
        public LevelEditorControl()
        {
            InitializeComponent();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string saveName = FileDialog.GetSaveFilename("Save Level", "lvl", "Level File");
                if (saveName != null)
                {
                    Editor.CurrentLevel.SaveLevel(saveName);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Exception when saving file: \n" + ex.ToString());
            }
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string loadName = FileDialog.GetOpenFilename("Load a level...", "lvl", "Level File");
                if(loadName != null)
                {
                    Level lvl = Level.LoadLevel(loadName);
                    Editor.SetLevel(lvl);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("Exception when loading file: \n" + ex.ToString());
            }
        }

        private void btnMove_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Move);
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Delete);
        }

        private void btnWall_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Wall);
        }

        private void btnParabola_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Parabola);
        }

        private void btnSpherical_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Spherical);
        }

        private void btnDecoration_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.Decoration);
        }

        private void btnSolid_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.ToggleSolid);
        }

        private void btnRefract_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.SetRefract);
        }

        private void btnEntrance_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.SetEntrance);
        }

        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.SetExit);
        }

        private void btnLaser_Click(object sender, RoutedEventArgs e)
        {
            Editor.SetTool(LevelEditorPanel.Tool.SetLaser);
        }

        private void btnTest_Click(object sender, RoutedEventArgs e)
        {
            GameAutomation.EnterTestLevel(Editor.CurrentLevel);
        }
    }
}
