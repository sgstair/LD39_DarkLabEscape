using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
    /// Interaction logic for GameLevelControl.xaml
    /// </summary>
    public partial class GameLevelControl : UserControl
    {
        DrawingVisual dv = new DrawingVisual();
        Level CurrentLevel = null;
        LevelRender CurrentRender = null;

        Timer GameTimer;

        public GameLevelControl()
        {
            AddVisualChild(dv);
            InitializeComponent();

            SizeChanged += GameLevelControl_SizeChanged;
            MouseMove += GameLevelControl_MouseMove;
            MouseLeftButtonDown += GameLevelControl_MouseLeftButtonDown;
            MouseLeftButtonUp += GameLevelControl_MouseLeftButtonUp;


            GameTimer = new Timer(GameTick);
            GameTimer.Change(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

            
        }


        Stopwatch timeHeld = new Stopwatch();
        bool leftDown = false;
        private void GameLevelControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            leftDown = false;
            timeHeld.Stop();
            Redraw();
        }

        private void GameLevelControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            leftDown = true;
            timeHeld.Restart();
        }

        private void GameLevelControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void GameLevelControl_MouseMove(object sender, MouseEventArgs e)
        {
            if(CurrentLevel != null)
            {
                Point mp = e.GetPosition(this);
                Point lp = CurrentRender.ScreenToLevel(mp);
                TestPath = CurrentLevel.TracePath(CurrentLevel.GenerateRayFromPoint(lp), 10);

                Redraw();
            }
        }

        RayPath TestPath = null;

        internal void SetLevel(Level levelContent)
        {
            CurrentLevel = levelContent;
            CurrentRender = new LevelRender(CurrentLevel);
            leftDown = false;
            TestPath = null;
            Redraw();
        }


        double GetLaserDistance()
        {
            return timeHeld.Elapsed.TotalSeconds * 2;
        }

        void GameTick(object state)
        {
            try
            {
                Dispatcher.Invoke(() => Update());
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
        }


        void Update()
        {
            if (CurrentLevel != null)
            {
                if (leftDown)
                {
                    // Check level win condition.
                    double laserDistance = GetLaserDistance();
                    TestPath.Trace(laserDistance);
                    
                    if(TestPath.HitTarget && laserDistance > TestPath.TracedDistance)
                    {
                        // Level win condition. (todo: fancy graphics & stuff)
                        GameAutomation.EnterLevel(CurrentLevel.LevelIndex + 1);
                        return;
                    }

                    Redraw();
                }
            }
        }

        public void Redraw()
        {
            DrawingContext dc = dv.RenderOpen();

            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (CurrentLevel != null)
            {
                Rect levelArea = new Rect(0, 0, ActualWidth, ActualHeight);
                CurrentRender.SetRegionRectangle(levelArea);
                if (leftDown)
                {
                    CurrentRender.SetLaserPath(TestPath, GetLaserDistance());
                }
                else
                {
                    CurrentRender.SetPreviewPath(TestPath);
                }

                CurrentRender.Render(dc);
            }


            dc.Close();
        }



        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new InvalidOperationException();
            return dv;
        }
        protected override int VisualChildrenCount
        {
            get
            {
                return 1;
            }
        }
        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (double.IsNaN(arrangeBounds.Width)) arrangeBounds.Width = 100;
            if (double.IsNaN(arrangeBounds.Height)) arrangeBounds.Height = 100;
            return arrangeBounds;
            
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (double.IsNaN(constraint.Width)) constraint.Width = 100;
            if (double.IsNaN(constraint.Height)) constraint.Height = 100;
            return constraint;
        }

    }
}
