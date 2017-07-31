using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        const double UiTop = 70;
        const double UiMargin = 30;
        const double PowerBarSize = 30;


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

        bool leftDown = false;
        private void GameLevelControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            leftDown = false;
            Redraw();
        }

        private void GameLevelControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            leftDown = true;
        }

        private void GameLevelControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }

        private void GameLevelControl_MouseMove(object sender, MouseEventArgs e)
        {
            if(CurrentLevel != null)
            {
                if (GameAutomation.Narration.GameInteractive)
                {
                    Point mp = e.GetPosition(this);
                    Point lp = CurrentRender.ScreenToLevel(mp);
                    CurrentLevel.SetDesiredVector(lp - CurrentLevel.LaserLocation);
                    Redraw();
                }
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

            LevelFinished = false;
            LevelWon = false;

            GameAutomation.Narration.Reset();
            GameAutomation.Narration.QueuePreLevelContent(levelContent.LevelIndex);

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
            GameAutomation.Narration.Reset();
            LevelTestMode = true;
        }


        double GetLaserDistance()
        {
            return CurrentLevel.LaserLength;
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

        bool LevelFinished;
        bool LevelWon;

        void Update()
        {
            try
            {
                if (CurrentLevel != null)
                {
                    // Update the level
                    double curTime = LevelTimer.Elapsed.TotalSeconds;
                    double dTime = curTime - UpdateLastTime;
                    UpdateLastTime = curTime;

                    GameAutomation.Narration.Update(dTime);

                    bool laserOn = leftDown && GameAutomation.Narration.GameInteractive;



                    CurrentLevel.UpdateLevel(dTime, laserOn);
                    TestPath = CurrentLevel.TracePath(CurrentLevel.GenerateRayFromAngle(CurrentLevel.LaserAngle), 10);

                    CurrentRender.Update(dTime);

                    if (laserOn)
                    {
                        // Check level win condition.
                        double laserDistance = GetLaserDistance();
                        TestPath.Trace(laserDistance);

                        if (TestPath.HitTarget && laserDistance > TestPath.TracedDistance)
                        {
                            // Apply power to the target
                            CurrentLevel.AppliedTargetPower = true;

                            if (CurrentLevel.TargetPower > Level.TargetPowerRequired)
                            {
                                // Level win condition. (todo: fancy graphics & stuff)
                                LevelFinished = true;
                                LevelWon = true;
                                CurrentLevel.Complete = true;
                                if (LevelTestMode)
                                {
                                    GameTimer.Change(Timeout.Infinite, Timeout.Infinite);
                                    GameAutomation.EnterEditor();
                                    return;
                                }
                                GameAutomation.Narration.QueuePostLevelContent(CurrentLevel.LevelIndex);
                            }
                        }

                    }

                    GameState state = GameAutomation.State;
                    state.Update(dTime, laserOn);

                    if (state.RemainingPower <= 0)
                    {
                        GameTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        // Todo: use narration for this path too.
                        // Lose condition
                        if (LevelTestMode)
                        {
                            GameAutomation.EnterEditor();
                        }
                        else
                        {
                            GameAutomation.LevelCompleteFailure();
                        }

                        return;
                    }


                    if(GameAutomation.Narration.LevelExit)
                    {
                        GameTimer.Change(Timeout.Infinite, Timeout.Infinite);
                        if (LevelTestMode)
                        {
                            GameAutomation.EnterEditor();
                        }
                        else
                        {
                            if (LevelWon)
                            {
                                GameAutomation.LevelCompleteSuccess();
                            }
                            else
                            {
                                GameAutomation.LevelCompleteFailure();
                            }

                        }
                        return;
                    }

                    Redraw();
                }
            }
            catch(Exception ex)
            {
                GameTimer.Change(Timeout.Infinite, Timeout.Infinite);
                MessageBox.Show("Exception in core game loop. Game is halted.\n" + ex.ToString());
            }
        }


        Typeface font = new Typeface("Calibri");
        public void Redraw()
        {
            if (ActualWidth == 0 || ActualHeight == 0) return;
            DrawingContext dc = dv.RenderOpen();

            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (CurrentLevel != null && ActualWidth > UiMargin*3 && ActualHeight > (UiMargin*2+UiTop))
            {

                // Draw power bar at the top, before the level - so particles can render above it.
                Rect PowerBarRect = new Rect(20, UiMargin, ActualWidth - 40, PowerBarSize);
                DrawPowerBar(dc, PowerBarRect);


                Rect levelArea = new Rect(UiMargin, UiTop, ActualWidth - UiMargin*2, ActualHeight - UiMargin - UiTop);
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


                // Draw Narration Box
                double BoxWidth = ActualWidth / 2;
                double BoxHeight = ActualHeight / 5;

                var n = GameAutomation.Narration;
                if(n.CurrentEvent != null)
                {
                    if(n.BoxVisiblePercent > 0.5)
                    {
                        Rect rc = new Rect(ActualWidth - 10 - BoxWidth, ActualHeight - 10 - BoxHeight, BoxWidth, BoxHeight);
                        Pen p = new Pen(Brushes.DarkBlue, 4);

                        dc.DrawRoundedRectangle(Brushes.White, p, rc, 10, 10);

                        int textCharacters = (int)Math.Round(n.CurrentEvent.Text.Length * n.TextDisplayPercent);
                        string displayText = n.CurrentEvent.Text.Substring(0, textCharacters);
                        FormattedText t = new FormattedText(displayText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, 24, Brushes.Black);
                        t.MaxTextWidth = BoxWidth - 20;
                        t.MaxTextHeight = BoxHeight - 20;

                        dc.DrawText(t, rc.TopLeft + new Vector(10, 10));

                    }
                }

                // Draw System Messages
                if(n.SystemTextMessage != null)
                {
                    FormattedText t = new FormattedText(n.SystemTextMessage, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, font, 48, Brushes.Red);


                    double y = ActualHeight / 3;
                    Point textPt = new Point((ActualWidth - t.Width) / 2, y);
                    double boxWidth = t.Width + 40;
                    double boxHeight = t.Height + 40;
                    Rect rc = new Rect(textPt + new Vector(-20, -20), new Size(boxWidth, boxHeight));


                    dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(74, 0, 0, 0)), null, rc);
                    dc.DrawText(t, textPt);

                }
            }


            dc.Close();
        }

        void DrawPowerBar(DrawingContext dc, Rect area)
        {
            GameState state = GameAutomation.State;

            dc.DrawRoundedRectangle(Brushes.White, null, area, 10, 10);

            Rect clearArea = new Rect(area.Left + 40, area.Top - 5, area.Width, area.Height);
            dc.DrawRectangle(Brushes.Black, null, clearArea);

            Rect barArea = new Rect(area.Left + 45, area.Top, area.Width - 45, area.Height - 10);
            dc.PushClip(new RectangleGeometry(area, 10, 10));

            

            Rect fullArea = new Rect(barArea.Left, barArea.Top, barArea.Width * state.RemainingPower / state.StartingPower, barArea.Height);
            dc.DrawRectangle(Brushes.Green, null, fullArea);
            if (state.PowerWhenLaserStarted > state.RemainingPower)
            {
                Rect deadArea = new Rect(fullArea.Right, barArea.Top, barArea.Width * (state.PowerWhenLaserStarted - state.RemainingPower) / state.StartingPower, barArea.Height);
                dc.DrawRectangle(Brushes.Red, null, deadArea);
            }


            dc.Pop();

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
