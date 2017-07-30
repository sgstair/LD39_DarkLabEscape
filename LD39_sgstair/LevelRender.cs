using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LD39_sgstair
{
    class LevelRender
    {
        public LevelRender(Level attachLevel, bool designMode = false)
        {
            CurrentLevel = attachLevel;
            DesignMode = designMode;
            LoadTileset();
            UpdateLevelArea();
        }
        Level CurrentLevel;
        Rect LevelBounds;
        RayPath DrawPath;
        bool LaserActive;
        double LaserDistance;
        Point[] LevelOutline;
        PathGeometry LevelOutlinePath;
        bool DesignMode;
        BitmapFrame TileData;

        void LoadTileset()
        {
            PngBitmapDecoder decoder = new PngBitmapDecoder(File.OpenRead("Tiles_Src.png"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            TileData = decoder.Frames[0];

        }


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

        public void UpdateLevelArea()
        {
            LevelBounds = CurrentLevel.LevelArea;
            if (LevelBounds.Width < 1) LevelBounds.Width = 1;
            if (LevelBounds.Height < 1) LevelBounds.Height = 1;

            // Determine level outline
            List<LevelRegion> regions = CurrentLevel.IdentifyRegions();
            LevelOutline = null;
            LevelOutlinePath = null;
            if(regions.Count > 0)
            {
                LevelRegion outlineRegion = regions.OrderByDescending(r => r.Area).FirstOrDefault();
                LevelOutline = outlineRegion.ClosedForm();
            }
        }


        Rect DrawRect;
        double Scale;
        Vector ScreenOffset;
        public void SetRegionRectangle(Rect newRegion, double scaleAdjust = 1)
        {
            DrawRect = newRegion;

            if (LevelBounds.Width == 0 || LevelBounds.Height == 0) return;
            if (newRegion.Width <= 0 || newRegion.Height <= 0) return;

            // compute offset and scale.
            double scalex = DrawRect.Width / LevelBounds.Width;
            double scaley = DrawRect.Height / LevelBounds.Height;
            Scale = Math.Min(scalex, scaley) * scaleAdjust;

            ScreenOffset.X = -LevelBounds.X * Scale + DrawRect.X + (DrawRect.Width - LevelBounds.Width * Scale) / 2;
            ScreenOffset.Y = -LevelBounds.Y * Scale + DrawRect.Y + (DrawRect.Height - LevelBounds.Height * Scale) / 2;


            // Create geometry for the clipping region
            if (LevelOutline != null)
            {
                Point[] TransformedLevelOutline = LevelOutline.Select(p => LevelToScreen(p)).ToArray();
                PolyLineSegment seg = new PolyLineSegment(TransformedLevelOutline, false);
                PathFigure fig = new PathFigure(TransformedLevelOutline[0], new PathSegment[] { seg }, false);
                LevelOutlinePath = new PathGeometry(new PathFigure[] { fig });
            }
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


        public void DrawGrid(DrawingContext dc)
        {
            for (int y = (int)Math.Floor(LevelBounds.Top - 1); y <= Math.Ceiling(LevelBounds.Bottom + 1); y++)
            {
                for (int x = (int)Math.Floor(LevelBounds.Left - 1); x <= Math.Ceiling(LevelBounds.Right + 1); x++)
                {
                    dc.DrawEllipse(Brushes.Blue, null, LevelToScreen(new Point(x, y)), 2, 2);
                }
            }
        }

        public void DrawTiles(DrawingContext dc)
        {
            for (int y = (int)Math.Floor(LevelBounds.Top); y < Math.Ceiling(LevelBounds.Bottom); y++)
            {
                for (int x = (int)Math.Floor(LevelBounds.Left); x < Math.Ceiling(LevelBounds.Right); x++)
                {
                    DrawTile(dc, new Point(x, y), 0);
                }
            }
        }

        public void Render(DrawingContext dc)
        {
            Pen linePen = new Pen(Brushes.White, 4);
            Pen rayPen = new Pen(Brushes.Green, 5);
            Pen rayCenterPen = new Pen(Brushes.White, 1);

            Pen scopePen = new Pen(Brushes.Red, 1);
            Pen highlightPen = new Pen(Brushes.Orange, 4);
            // hack rendering in for now.

            if (!DesignMode && LevelOutlinePath != null)
            {
                // Clip level to a path if we have one.
                dc.PushClip(LevelOutlinePath);
            }

            // Draw backdrop of tiles.
            DrawTiles(dc);


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

            if (!DesignMode && LevelOutlinePath != null)
            {
                dc.Pop();
            }
        }

        public void DrawTile(DrawingContext dc, Point levelLocation, int tile, double rotation = 0)
        {
            double targetSize = Scale;
            Point screenLocation = LevelToScreen(levelLocation);
            dc.PushTransform(new TranslateTransform(screenLocation.X + Scale / 2, screenLocation.Y + Scale / 2));
            if (rotation != 0)
                dc.PushTransform(new RotateTransform(rotation));
            dc.PushClip(new RectangleGeometry(new Rect(-targetSize / 2, -targetSize / 2, targetSize, targetSize)));
            double tileScale = Scale / 62; // Only use 62x62 region of 64x64 tile
            dc.PushTransform(new ScaleTransform(tileScale, tileScale));
            // Select the approriate tile by location
            double tx = (tile & 7) * 64 + 32;
            double ty = (tile / 8) * 64 + 32;
            dc.DrawImage(TileData, new Rect(-tx, -ty, TileData.PixelWidth, TileData.PixelHeight));

            dc.Pop();
            dc.Pop();
            dc.Pop();
            if (rotation != 0)
                dc.Pop();
        }

        public void HighlightFeature(DrawingContext dc, LevelFeature f)
        {
            Pen highlightPen = new Pen(Brushes.Orange, 4);
            dc.DrawLine(highlightPen, LevelToScreen(f.p1), LevelToScreen(f.p2));
        }

        public void HighlightPoint(DrawingContext dc, Point p)
        {
            dc.DrawEllipse(Brushes.Orange, null, LevelToScreen(p), 5, 5);
        }

        public void DrawCursor(DrawingContext dc, Point p)
        {
            dc.DrawEllipse(Brushes.Green, null, LevelToScreen(p), 5, 5);
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
