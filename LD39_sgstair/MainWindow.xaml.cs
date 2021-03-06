﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
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

        public void SetContent(FrameworkElement e, bool Stretch = true)
        {
            grid.Children.Clear();
            if (Stretch)
            {
                // Stretch used for WPF/UI children
                e.HorizontalAlignment = HorizontalAlignment.Stretch;
                e.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                // Non-stretch for rendering stuff.
                e.HorizontalAlignment = HorizontalAlignment.Left;
                e.VerticalAlignment = VerticalAlignment.Top;
            }
            e.Margin = new Thickness(0);
            grid.Children.Add(e);
        }
    }

    class GameAutomation
    {
        public static SoundEngine Sound;
        public static NarrationSystem Narration;

        const int LevelCount = 6;

        static MainWindow parentWindow;
        static GameMenuControl menuControl = new GameMenuControl();
        static GameLevelControl levelControl = new GameLevelControl();
        static LevelEditorControl editorControl = new LevelEditorControl();
        static GameOverControl gameoverControl = new GameOverControl();

        static Level currentLevel;

        public static GameState State = new GameState();

        public static void PrepareGame(MainWindow bindMainWindow)
        {
            parentWindow = bindMainWindow;
            WindowInteropHelper interop = new WindowInteropHelper(parentWindow);
            Sound = new SoundEngine(interop.EnsureHandle());
            Narration = new NarrationSystem();
            // Future: maybe show pre-menu slides?

            EnterMenu();
        }

        public static void StartNewGame()
        {
            State = new GameState();
            EnterLevel(0);
        }

        public static void LevelCompleteSuccess()
        {
            int currentLevelIndex = currentLevel.LevelIndex;
            currentLevelIndex++;
            if(currentLevelIndex == LevelCount)
            {
                // Won game
                State.WonGame = true;
                EnterGameOverScreen();
            }
            else
            {
                // Next level
                EnterLevel(currentLevelIndex);
            }
        }

        public static void LevelCompleteFailure()
        {
            State.WonGame = false;
            EnterGameOverScreen();
        }


        public static void EnterLevel(int level)
        {
            Sound.SoftReset();
            currentLevel = GetLevel(level);
            levelControl.SetLevel(currentLevel);
            parentWindow.SetContent(levelControl, false);
        }
        public static void EnterMenu()
        {
            Sound.SoftReset();
            parentWindow.SetContent(menuControl);
        }

        public static void EnterEditor()
        {
            Sound.SoftReset();
            parentWindow.SetContent(editorControl, false);
        }

        public static void EnterGameOverScreen()
        {
            Sound.SoftReset();
            gameoverControl.UpdateText();
            parentWindow.SetContent(gameoverControl);
        }

        public static void EnterTestLevel(Level lvl)
        {
            State = new GameState();
            levelControl.SetTestLevel(lvl);
            parentWindow.SetContent(levelControl);
        }


        public static void ExitGame()
        {
            Sound.SoftReset();
            System.Threading.Thread.Sleep(100);
            parentWindow.Close();
        }


        public static Level GetLevel(int index)
        {
            if(index >= 0 && index < LevelCount)
            {
                Level l = Level.LoadLevel($"Level{index}.lvl");
                l.LevelIndex = index;
                return l;
            }
            
            return null;
        }
    }

    class GameState
    {
        public const double InitialPower = 30 * 60; // 30 minutes of power
        public const double LaserUseRate = 15; // When the laser is active, use 15x the baseline power.

        public GameState()
        {
            StartingPower = RemainingPower = InitialPower;
            PowerWhenLaserStarted = RemainingPower;
        }

        public double LastPercent = 0.99;

        public void Update(double timeElapsed, bool laserOn)
        {
            if(laserOn && !lastLaserOn)
            {
                PowerWhenLaserStarted = RemainingPower;
            }

            if(laserOn)
            {
                RemainingPower -= timeElapsed * LaserUseRate;
                LaserPowerHoldTime = 3;
                LaserOnTime += timeElapsed;
                LaserSessionTime += timeElapsed;
            }
            else
            {
                LaserSessionTime = 0;
                if (!DrainPaused)
                {
                    RemainingPower -= timeElapsed;
                }
                LaserPowerHoldTime -= timeElapsed;
                if(LaserPowerHoldTime < 0)
                {
                    LaserPowerHoldTime = 0;
                    PowerWhenLaserStarted -= LaserUseRate * timeElapsed * 4;
                    if (PowerWhenLaserStarted < RemainingPower) PowerWhenLaserStarted = RemainingPower;
                }
            }
            lastLaserOn = laserOn;

            double curPercent = RemainingPower / StartingPower;
            int lastPercentDecade = (int)Math.Floor(LastPercent * 10);
            int curPercentDecade = (int)Math.Floor(curPercent * 10);
            if(curPercentDecade < lastPercentDecade)
            {
                // Initiate system message warning of the reduced power
                GameAutomation.Narration.DisplaySystemText($"Backup power at {lastPercentDecade}0%");
            }
            LastPercent = curPercent;
        }

        bool lastLaserOn = false;

        public double StartingPower;
        /// <summary>
        /// Power is measured in terms of time (time it takes to deplete the backup power of the facility)
        /// </summary>
        public double RemainingPower;

        /// <summary>
        /// Snapshot of what the remaining power was when the laser started operation, animates down to RemainingPower after the laser stops.
        /// </summary>
        public double PowerWhenLaserStarted;

        public double LaserPowerHoldTime;

        public double LaserSessionTime;

        public bool ScriptedOff = false;
        public bool DrainPaused = false;

        public double LightLevel
        {
            get
            {
                // Base light level
                double baseLight = 0.2 + (RemainingPower / StartingPower)*0.8;

                
                if(LaserSessionTime > 0 && LaserSessionTime < 0.2)
                {
                    double beamReduce = Math.Sin(LaserSessionTime / 0.2 * Math.PI);
                    baseLight = baseLight * (1 - beamReduce * 0.4); // Power dip due to starting laser.
                }

                if (ScriptedOff) baseLight = 0.1;
                return baseLight;
            }
        }

        // Also use this class as the place to hold information about the state of the game.

        public double LaserOnTime = 0;
        public double LaserItemDamage = 0;
        public double EncounteredItemValue = 0;
        public double LaserWallDamage = 0;
        public int LevelsCompleted = 0;
        public bool WonGame = false;
    }

}
