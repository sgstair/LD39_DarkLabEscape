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

        public const int FirstDecorationTile = 16;
        public const int NumDecorationTiles = 1;

        public static readonly Color BeamColor = Color.FromArgb(255, 13, 249, 76);
        public static readonly Color BeamHighlight = Color.FromArgb(255, 255, 255, 255);
        public static readonly Color BeamRunner1 = Color.FromArgb(255, 49, 122, 236);
        public static readonly Color BeamRunner2 = Color.FromArgb(255, 227, 63, 242);

        public static readonly double LaserOffset = 0.45;


        public LevelRender(Level attachLevel, bool designMode = false)
        {
            CurrentLevel = attachLevel;
            DesignMode = designMode;
            LoadTileset();
            UpdateLevelArea();
            Particles = new ParticleSystem(this);
            Particles.AddEmitter(new ParticleEmitter() { Location = new Point(3, 3), Type = ParticleType.BeamReflect, Value = 0.1 });
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
        ParticleSystem Particles;


        void LoadTileset()
        {
            PngBitmapDecoder decoder = new PngBitmapDecoder(File.OpenRead("Tiles_Src.png"), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            TileData = decoder.Frames[0];

        }

        List<ParticleEmitter> PathEmitters = new List<ParticleEmitter>();
        void SetPathLength(int usedEmitters)
        {
            if (usedEmitters < PathEmitters.Count)
            {
                // Remove any extra emitters
                for (int i = usedEmitters; i < PathEmitters.Count; i++)
                {
                    Particles.RemoveEmitter(PathEmitters[i]);
                }
                PathEmitters.RemoveRange(usedEmitters, PathEmitters.Count - usedEmitters);
            }
        }
        void SetPathEmitter(int index, Point location)
        {
            if(PathEmitters.Count <= index)
            {
                PathEmitters.Add(new ParticleEmitter() { Type = ParticleType.BeamReflect });
                Particles.AddEmitter(PathEmitters.Last());
            }
            PathEmitters[index].Location = location;
        }

        ParticleEmitter LaserEmitter = null;
        ParticleEmitter TargetEmitter = null;

        void DestroyTargetEmitter()
        {
            if(TargetEmitter != null)
            {
                Particles.RemoveEmitter(TargetEmitter);
                TargetEmitter = null;
            }
        }
        void CreateTargetEmitter(Point location)
        {
            if(TargetEmitter == null)
            {
                TargetEmitter = new ParticleEmitter() { Type = ParticleType.HitTarget};
                Particles.AddEmitter(TargetEmitter);
            }
            TargetEmitter.Location = location;
        }

        void CreateLaserEmitter(Point location, double percentActive)
        {
            if(LaserEmitter == null)
            {
                LaserEmitter = new ParticleEmitter() { Type = ParticleType.BeamStart };
                Particles.AddEmitter(LaserEmitter);
            }
            LaserEmitter.Location = location;
            LaserEmitter.Value = percentActive * 0.12;
        }
        void DestroyLaserEmitter()
        {
            if(LaserEmitter != null)
            {
                Particles.RemoveEmitter(LaserEmitter);
                LaserEmitter = null;
            }
        }


        public void SetLaserPath(RayPath p, double laserDistance)
        {
            LaserDistance = laserDistance;
            LaserActive = true;
            DrawPath = p;
            DrawPath.Trace(laserDistance); // Ensure we have enough distance
            // Prepare particle emitters
            int emitters = 0;
            bool hitTarget = false;
            foreach(RayInteraction ri in p.Interactions)
            {
                if(ri.RayIn != null && (ri.RayIn.PreviousDistance + ri.RayIn.Length) < laserDistance )
                {
                    if(ri.InteractionFeature != null && ri.InteractionFeature.WillReflect)
                    {
                        SetPathEmitter(emitters++, ri.RayOut.Origin); // There will always be a ray out from a reflection.
                    }
                    else if(ri.HitTarget)
                    {
                        hitTarget = true;
                        CreateTargetEmitter(ri.RayIn.Origin + ri.RayIn.Direction * ri.RayIn.Length);
                    }
                }
            }
            SetPathLength(emitters);
            if (!hitTarget) DestroyTargetEmitter();

            double activePercent = Math.Min(1, laserDistance / LaserOffset);
            CreateLaserEmitter(p.Interactions[0].RayOut.Origin + p.Interactions[0].RayOut.Direction * LaserOffset, activePercent);
        }
        public void SetPreviewPath(RayPath p)
        {
            DrawPath = p;
            LaserActive = false;
            SetPathLength(0);
            DestroyTargetEmitter();
            DestroyLaserEmitter();
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
        public double Scale;
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

        public void Update(double elapsedTime)
        {
            Particles.UpdateParticles(elapsedTime);
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


            DrawTileCentered(dc, CurrentLevel.LaserLocation, 8, CurrentLevel.LaserAngle - Math.PI/2);

            //dc.DrawEllipse(Brushes.Blue, null, LevelToScreen(CurrentLevel.LaserLocation), 10, 10);
            //dc.DrawEllipse(Brushes.Red, null, LevelToScreen(CurrentLevel.TargetLocation), 8, 8);

            foreach (LevelFeature f in CurrentLevel.ActiveFeatures)
            {
                DrawFeature(dc,f);
            }
            // Draw the wall joining points at all of the corner points
            foreach(Point p in CurrentLevel.ActiveFeatures.Select(f => f.p1).Concat(CurrentLevel.ActiveFeatures.Select(f => f.p2)).Distinct())
            {
                DrawTileCentered(dc, p, 5);
            }


            foreach(LevelDecoration decoration in CurrentLevel.Decorations)
            {
                DrawDecoration(dc, decoration);
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

            Particles.RenderParticles(dc);
        }

        public void DrawDecoration(DrawingContext dc, LevelDecoration dec)
        {
            DrawTileCentered(dc, dec.p, dec.DecorationIndex + FirstDecorationTile, dec.Rotation);
        }


        public void DrawTileCentered(DrawingContext dc, Point levelLocation, int tile, double rotation = 0)
        {
            DrawTile(dc, levelLocation - new Vector(0.5,0.5), tile, rotation);
        }

        public void DrawTile(DrawingContext dc, Point levelLocation, int tile, double rotation = 0)
        {
            double targetSize = Scale;
            Point screenLocation = LevelToScreen(levelLocation);
            dc.PushTransform(new TranslateTransform(screenLocation.X + Scale / 2, screenLocation.Y + Scale / 2));
            if (rotation != 0)
                dc.PushTransform(new RotateTransform(-rotation * 180 / Math.PI));
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
        public void DrawTileClipWidth(DrawingContext dc, Point levelLocation, int tile, double rotation = 0, double width = 1)
        {
            double targetSize = Scale;
            Point screenLocation = LevelToScreen(levelLocation);
            dc.PushTransform(new TranslateTransform(screenLocation.X + Scale / 2, screenLocation.Y + Scale / 2));
            if (rotation != 0)
                dc.PushTransform(new RotateTransform(-rotation * 180 / Math.PI));
            dc.PushClip(new RectangleGeometry(new Rect(-targetSize * width / 2, -targetSize / 2, targetSize * width, targetSize)));
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


        public void DrawFeature(DrawingContext dc, LevelFeature f)
        {
            if (f is LevelEntryExitDoor)
            {
                Vector v = f.p2 - f.p1;
                double length = v.Length;
                Point center = f.p1 + v / 2;
                double angle = Math.Atan2(-v.Y, v.X);
                v.Normalize();
                // Draw a door at the center
                DrawTileCentered(dc, center, 13, angle); // Door
                DrawTileCentered(dc, center, 12, angle); // Gap in wall for door

                // Draw the walls on either side.
                if (length > 1)
                {
                    DrawWall(dc, f.p1, f.p1 + v * (length - 1) / 2);
                    DrawWall(dc, f.p2 - v * (length - 1) / 2, f.p2);
                }

                // Draw the target location on top of the wall, on both sides
                int targetTile = 11;
                if (((LevelEntryExitDoor)f).ExitDoor == false) targetTile = 10;

                center -= v;
                DrawTileCentered(dc, center, targetTile, angle);
                DrawTileCentered(dc, center, targetTile, angle + Math.PI);
            }
            else
            {
                DrawWall(dc, f.p1, f.p2);
            }
        }

        public void DrawWall(DrawingContext dc, Point p1, Point p2)
        {
            double lineLength = (p2 - p1).Length;
            Vector fwd = (p2 - p1);
            fwd.Normalize();
            while(lineLength > 0)
            {
                double drawLength = Math.Min(lineLength, 1);
                p2 = p1 + fwd * drawLength;
                DrawWallSegment(dc, p1, p2);

                p1 = p2;
                lineLength -= drawLength;
            }
        }

        public void DrawWallSegment(DrawingContext dc, Point p1, Point p2)
        {
            double length = (p2 - p1).Length;
            Point center = p1 + (p2 - p1) / 2;
            Vector v = p2 - p1;
            double angle = Math.Atan2(-v.Y, v.X);
            DrawTileClipWidth(dc, center - new Vector(0.5,0.5), 4, angle, length);
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
