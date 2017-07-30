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
            KeyDown += GameLevelControl_KeyDown;

            GameTimer = new Timer(GameTick);
            GameTimer.Change(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

            
        }

        private void GameLevelControl_KeyDown(object sender, KeyEventArgs e)
        {
            switch(e.Key)
            {
                case Key.Escape:
                    if(LevelTestMode)
                    {
                        GameAutomation.EnterEditor();
                        return;
                    }
                    break;
            }
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
                CurrentLevel.SetDesiredVector(lp - CurrentLevel.LaserLocation);
                Redraw();
            }
        }

        RayPath TestPath = null;
        bool LevelTestMode = false;

        internal void SetLevel(Level levelContent)
        {
            LevelTestMode = false;
            GameTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CurrentLevel = levelContent;
            CurrentLevel.StartLevel();
            CurrentRender = new LevelRender(CurrentLevel);
            leftDown = false;
            TestPath = null;

            LevelTimer = new Stopwatch();
            LevelTimer.Start();
            UpdateLastTime = 0;
            GameTick(null);
            GameTimer.Change(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

            Redraw();
        }

        internal void SetTestLevel(Level levelContent)
        {
            SetLevel(levelContent);
            LevelTestMode = true;
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


        Stopwatch LevelTimer;
        double UpdateLastTime;

        void Update()
        {
            if (CurrentLevel != null)
            {
                // Update the level
                double curTime = LevelTimer.Elapsed.TotalSeconds;
                double dTime = curTime - UpdateLastTime;
                UpdateLastTime = curTime;

                CurrentLevel.UpdateLevel(dTime);
                TestPath = CurrentLevel.TracePath(CurrentLevel.GenerateRayFromAngle(CurrentLevel.LaserAngle), 10);

                CurrentRender.Update(dTime);

                if (leftDown)
                {
                    // Check level win condition.
                    double laserDistance = GetLaserDistance();
                    TestPath.Trace(laserDistance);
                    
                    if(TestPath.HitTarget && laserDistance > TestPath.TracedDistance)
                    {
                        // Apply power to the target
                        CurrentLevel.AppliedTargetPower = true;

                        if (CurrentLevel.TargetPower > 4)
                        {
                            // Level win condition. (todo: fancy graphics & stuff)
                            if (LevelTestMode)
                            {
                                GameAutomation.EnterEditor();
                            }
                            else
                            {
                                GameAutomation.EnterLevel(CurrentLevel.LevelIndex + 1);
                            }
                        }
                        return;
                    }

                }
                Redraw();
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
