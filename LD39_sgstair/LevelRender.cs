using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LD39_sgstair
{
    class LevelRender
    {
        public LevelRender(Level attachLevel)
        {
            CurrentLevel = attachLevel;
            LevelBounds = CurrentLevel.LevelArea;
        }
        Level CurrentLevel;
        Rect LevelBounds;
        RayPath DrawPath;
        bool LaserActive;
        double LaserDistance;

        public void SetLaserPath(RayPath p, double laserDistance)
        {
            LaserDistance = laserDistance;
            LaserActive = true;
            DrawPath = p;
        }
        public void SetPreviewPath(RayPath p)
        {
            DrawPath = p;
            LaserActive = false;
        }


        Rect DrawRect;
        double Scale;
        Vector ScreenOffset;
        public void SetRegionRectangle(Rect newRegion)
        {
            DrawRect = newRegion;

            if (LevelBounds.Width == 0 || LevelBounds.Height == 0) return;
            if (newRegion.Width <= 0 || newRegion.Height <= 0) return;

            // compute offset and scale.
            double scalex = DrawRect.Width / LevelBounds.Width;
            double scaley = DrawRect.Height / LevelBounds.Height;
            Scale = Math.Min(scalex, scaley);

            ScreenOffset.X = DrawRect.X + (DrawRect.Width - LevelBounds.Width * Scale) / 2;
            ScreenOffset.Y = DrawRect.Y + (DrawRect.Height - LevelBounds.Height * Scale) / 2;
        }

        public Point LevelToScreen(Point level)
        {
            level.X *= Scale;
            level.Y *= Scale;
            return level + ScreenOffset;
        }

        public Point ScreenToLevel(Point screen)
        {
            screen -= ScreenOffset;
            screen.X /= Scale;
            screen.Y /= Scale;
            return screen;
        }


        public void Render(DrawingContext dc)
        {
            Pen linePen = new Pen(Brushes.White, 4);
            Pen rayPen = new Pen(Brushes.Green, 5);
            Pen rayCenterPen = new Pen(Brushes.White, 1);

            Pen scopePen = new Pen(Brushes.Red, 1);
            Pen highlightPen = new Pen(Brushes.Orange, 4);
            // hack rendering in for now.

            dc.DrawEllipse(Brushes.Blue, null, LevelToScreen(CurrentLevel.LaserLocation), 10, 10);
            dc.DrawEllipse(Brushes.Red, null, LevelToScreen(CurrentLevel.TargetLocation), 8, 8);

            foreach (LevelFeature f in CurrentLevel.ActiveFeatures)
            {
                dc.DrawLine(linePen, LevelToScreen(f.p1), LevelToScreen(f.p2));
            }

            if (DrawPath != null)
            {
                if (LaserActive)
                {
                    DrawPath.Trace(LaserDistance);
                    DrawLaserRay(dc, DrawPath, rayPen, LaserDistance);
                    DrawLaserRay(dc, DrawPath, rayCenterPen, LaserDistance);
                }
                else
                {
                    DrawLaserRay(dc, DrawPath, scopePen, 5);
                }

            }


        }

        void DrawLaserRay(DrawingContext dc, RayPath path, Pen p, double maxDistance)
        {
            foreach (RayInteraction ri in DrawPath.Interactions)
            {
                if (ri.RayOut != null)
                {
                    double drawLength = ri.RayOut.Length;
                    if (drawLength > maxDistance) drawLength = maxDistance;
                    Point p1 = ri.RayOut.Origin;
                    Point p2 = p1 + ri.RayOut.Direction * drawLength;
                    dc.DrawLine(p, LevelToScreen(p1), LevelToScreen(p2));

                    maxDistance -= ri.RayOut.Length;
                    if (maxDistance <= 0) break;
                }
            }
        }
    }
}
