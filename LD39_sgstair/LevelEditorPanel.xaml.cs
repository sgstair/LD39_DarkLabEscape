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
    /// Interaction logic for LevelEditorPanel.xaml
    /// </summary>
    public partial class LevelEditorPanel : UserControl
    {
        const double HighlightDistance = 0.3;
        const double SnapDistance = 0.3;

        DrawingVisual dv = new DrawingVisual();
        internal Level CurrentLevel = null;
        LevelRender CurrentRender = null;

        List<LevelRegion> LevelRegions = null;

        public LevelEditorPanel()
        {
            AddVisualChild(dv);
            InitializeComponent();
            MouseMove += LevelEditorPanel_MouseMove;
            MouseLeftButtonDown += LevelEditorPanel_MouseLeftButtonDown;
            MouseLeftButtonUp += LevelEditorPanel_MouseLeftButtonUp;
            SizeChanged += LevelEditorPanel_SizeChanged;
            

            SetLevel(LevelGenerator.GenerateLevel(Guid.NewGuid()));
            SetTool(Tool.Move);
        }

        private void LevelEditorPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Redraw();
        }


        private void LevelEditorPanel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            switch (CurrentTool)
            {
                case Tool.Move:
                    // When a move is complete, snap both ends of the feature to help reduce numerical instability / unclosable figures.
                    if (HighlightedFeature != null)
                    {
                        HighlightedFeature.p1 = SnapPoint(HighlightedFeature.p1);
                        HighlightedFeature.p2 = SnapPoint(HighlightedFeature.p2);
                    }
                    else if(HighlightedPoint != null)
                    {
                        foreach(LevelFeature f in MovingFeatures.Keys)
                        {
                            f.ResetNormal();
                        }
                    }
                    break;

                case Tool.Wall:
                    // Complete add
                    if (Adding)
                    {
                        if (AddFeature.p1 != AddFeature.p2)
                        {
                            // Don't add empty segments
                            CurrentLevel.ActiveFeatures.Add(AddFeature);
                        }
                    }
                    Adding = false;
                    AddFeature = null;
                    break;
            }
            Moving = false;
            CurrentRender.UpdateLevelArea();
            LevelRegions = CurrentLevel.IdentifyRegions();
            Redraw();
        }

        private void LevelEditorPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            switch (CurrentTool)
            {
                case Tool.Move:
                    MoveStart = CurrentRender.ScreenToLevel(e.GetPosition(this));
                    Moving = true;
                    if (HighlightedFeature != null)
                    {
                        OriginalPosition = HighlightedFeature.p1;
                    }
                    else if (HighlightedPoint != null)
                    {
                        OriginalPosition = HighlightedPoint.Value;
                        // Determine the set of elements that will be affected by this
                        MovingFeatures.Clear();
                        foreach (LevelFeature f in CurrentLevel.ActiveFeatures)
                        {
                            if (f.p1 == OriginalPosition) MovingFeatures.Add(f, 1);
                            if (f.p2 == OriginalPosition) MovingFeatures.Add(f, 2);
                        }
                    }
                    else
                    {
                        Moving = false; // Nothing to move.
                    }
                    break;

                case Tool.Delete:
                    // Remove highlighted element
                    if (HighlightedFeature != null)
                    {
                        CurrentLevel.ActiveFeatures.Remove(HighlightedFeature);
                        RemoveHighlight();
                    }
                    break;

                case Tool.Wall:
                    Point p = SnapPoint(CurrentRender.ScreenToLevel(e.GetPosition(this)));
                    AddFeature = new LevelFeature() { p1 = p, p2 = p };
                    Adding = true;
                    break;


                case Tool.SetEntrance:
                    break;
                case Tool.SetExit:
                    break;
                case Tool.SetLaser:
                    if (CursorPoint != null)
                    {
                        CurrentLevel.LaserLocation = CursorPoint.Value;
                    }
                    break;

            }

            Redraw();
        }

        /// <summary>
        /// Track the set of elements to modify when moving a point.
        /// </summary>
        Dictionary<LevelFeature, int> MovingFeatures = new Dictionary<LevelFeature, int>();

        Point OriginalPosition;
        Point MoveStart;
        bool Moving;
        bool Adding;

        private void LevelEditorPanel_MouseMove(object sender, MouseEventArgs e)
        {
            Point levelPoint = CurrentRender.ScreenToLevel(e.GetPosition(this));
            switch(CurrentTool)
            {
                case Tool.Move:
                    if (Moving)
                    {
                        Point newPosition = OriginalPosition + (levelPoint - MoveStart);
                        newPosition = SnapPoint(newPosition);

                        if(HighlightedFeature != null)
                        {
                            // Translate both ends of the feature
                            Vector delta = newPosition - HighlightedFeature.p1;
                            HighlightedFeature.p1 += delta;
                            HighlightedFeature.p2 += delta;
                        }
                        else if(HighlightedPoint != null)
                        {
                            // Only move points if they're sufficiently far away to not collapse the element.
                            foreach(var kvp in MovingFeatures)
                            {
                                if(kvp.Value == 1)
                                {
                                    if((newPosition-kvp.Key.p2).Length > HighlightDistance)
                                    {
                                        kvp.Key.p1 = newPosition;
                                    }
                                }
                                else
                                {
                                    if ((newPosition - kvp.Key.p1).Length > HighlightDistance)
                                    {
                                        kvp.Key.p2 = newPosition;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        HighlightPointElement(levelPoint);
                    }
                    break;

                case Tool.Delete:
                case Tool.ToggleSolid:
                    HighlightElement(levelPoint);
                    break;

                case Tool.Wall:
                    SetCursor(levelPoint);
                    if (Adding)
                    {
                        AddFeature.p2 = SnapPoint(levelPoint);
                    }
                    break;
                case Tool.Parabola:
                case Tool.Spherical:
                    SetCursor(levelPoint);
                    break;

                case Tool.Decoration:
                    break;

                case Tool.SetRefract:
                    // Find enclosing region
                    break;

                case Tool.SetEntrance:
                case Tool.SetExit:
                    break;
                case Tool.SetLaser:
                    SetLaserCursor(levelPoint);
                    break;

            }
            Redraw();
        }

        void RemoveHighlight()
        {
            HighlightedFeature = null;
            HighlightedPoint = null;
        }
        void RemoveCursor()
        {
            CursorPoint = null;
        }

        void HighlightElement(Point levelLocation)
        {
            RemoveHighlight();
            SetCursor(levelLocation);


            // Find an element near the cursor within the HighlightDistance
            foreach (LevelFeature f in CurrentLevel.ActiveFeatures)
            {
                double distance = f.DistanceToPoint(levelLocation);
                if (distance <= HighlightDistance)
                {
                    if (HighlightedFeature == null || distance < HighlightedDistance)
                    {
                        HighlightedDistance = distance;
                        HighlightedFeature = f;
                        RemoveCursor();
                    }
                }
            }

        }
        void HighlightPointElement(Point levelLocation)
        {
            HighlightElement(levelLocation);

            // See if there's a closer point.
            var points = CurrentLevel.ActiveFeatures.Select(f => f.p1).Concat(CurrentLevel.ActiveFeatures.Select(f => f.p2)).Distinct();

            foreach(Point p in points)
            {
                double distance = (levelLocation - p).Length;
                if(distance < HighlightDistance)
                {
                    if((HighlightedFeature == null && HighlightedPoint == null) || distance <= HighlightedDistance)
                    {
                        HighlightedFeature = null;
                        HighlightedPoint = p;
                        HighlightedDistance = distance;
                        RemoveCursor();
                    }
                }
            }
        }

        void SetCursor(Point levelLocation)
        {
            CursorPoint = SnapPoint(levelLocation);
        }
        void SetLaserCursor(Point levelLocation)
        {
            CursorPoint = SnapLaser(levelLocation);
        }

        void HighlightRegion(Point levelLocation)
        {
            // todo
        }
        double HighlightedDistance;
        LevelFeature HighlightedFeature;
        Point? HighlightedPoint;
        Point? CursorPoint;

        LevelFeature AddFeature;

        Point SnapPoint(Point levelPoint)
        {
            // Find nearest grid snap point
            Point gridSnap = new Point(Math.Round(levelPoint.X), Math.Round(levelPoint.Y));

            // Also provide option to snap to other things of interest (focal points / planes)

            if ((levelPoint - gridSnap).Length < SnapDistance) return gridSnap;
            return levelPoint;
        }

        Point SnapLaser(Point levelPoint)
        {
            // Find nearest grid snap point
            Point gridSnap = new Point(Math.Round(levelPoint.X*2)/2, Math.Round(levelPoint.Y*2)/2);

            // Also provide option to snap to other things of interest (focal points / planes)

            if ((levelPoint - gridSnap).Length < SnapDistance) return gridSnap;
            return levelPoint;
        }


        internal void SetLevel(Level levelContent)
        {
            CurrentLevel = levelContent;
            CurrentRender = new LevelRender(CurrentLevel, true);
            LevelRegions = CurrentLevel.IdentifyRegions();
            Redraw();
        }


        public void SetTool(Tool t)
        {
            RemoveHighlight();
            RemoveCursor();
            CurrentTool = t;
            Redraw();
        }

        public enum Tool
        {
            Move,
            Delete,
            Wall,
            Parabola,
            Spherical,
            Decoration,
            ToggleSolid,
            SetRefract,
            SetEntrance,
            SetExit,
            SetLaser
        }

        Tool CurrentTool;



        public void Redraw()
        {
            DrawingContext dc = dv.RenderOpen();

            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (CurrentLevel != null)
            {
                Rect levelArea = new Rect(0, 0, ActualWidth, ActualHeight);
                CurrentRender.SetRegionRectangle(levelArea, 0.7);

                CurrentRender.DrawGrid(dc);
                CurrentRender.Render(dc);

                if (!Moving)
                {
                    if (HighlightedFeature != null) CurrentRender.HighlightFeature(dc, HighlightedFeature);
                    if (HighlightedPoint != null) CurrentRender.HighlightPoint(dc, HighlightedPoint.Value);
                }

                if(Adding)
                {
                    CurrentRender.HighlightFeature(dc, AddFeature);
                }

                if(CursorPoint != null)
                {
                    CurrentRender.DrawCursor(dc, CursorPoint.Value);
                }
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
