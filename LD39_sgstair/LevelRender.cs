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

        public static readonly double LaserOffset = 0.7;


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




        public void SetLaserPath(RayPath p, double laserDistance)
        {
            LaserDistance = laserDistance;
            LaserActive = true;
            DrawPath = p;
            DrawPath.Trace(laserDistance); // Ensure we have enough distance
            // Prepare particle emitters

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
                center -= v;
                DrawTileCentered(dc, center, 11, angle);
                DrawTileCentered(dc, center, 11, angle + Math.PI);
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


    enum ParticleType
    {
        BeamStart,
        BeamReflect,
        Smoke,
        Fire,
        HitTarget
    }
    class ParticleEmitter
    {
        public Point Location;
        public ParticleType Type;
        public double Time;
        public double Value;
    }
    class ParticleSystem
    {
        public ParticleSystem(LevelRender r)
        {
            Render = r;
        }
        LevelRender Render;

        const int MaxParticles = 800;

        struct Particle
        {
            public Point Location;
            public Vector Velocity;
            public double Age;
            public double Lifespan;
            public double Phase;
            public Color ParticleColor;
            public double SizeH, SizeV;
            public IParticleBehavior Type;
            public ParticleEmitter Emitter;
        }

        Particle[] AllParticles = new Particle[MaxParticles];
        int ActiveParticles = 0;
        Random r = new Random();
        List<ParticleEmitter> Emitters = new List<ParticleEmitter>();

        public void AddEmitter(ParticleEmitter e)
        {
            Emitters.Add(e);
        }
        public void RemoveEmitter(ParticleEmitter e)
        {
            Emitters.Remove(e);
        }

        public void UpdateParticles(double timeElapsed)
        {
            // Age out old particles.
            for (int i = 0; i < ActiveParticles; i++)
            {
                AllParticles[i].Age += timeElapsed;
                if (AllParticles[i].Age > AllParticles[i].Lifespan)
                {
                    RemoveParticle(i);
                    i--; // recompute the same slot
                }
            }
            // Process emitters
            foreach(ParticleEmitter e in Emitters)
            {
                ProcessEmitter(e, timeElapsed);
            }

            // Update particles
            for (int i = 0; i < ActiveParticles; i++)
            {
                AllParticles[i].Type.Update(ref AllParticles[i]);
                AllParticles[i].Location += AllParticles[i].Velocity * timeElapsed;
            }
        }

        public void RenderParticles(DrawingContext dc)
        {
            for (int i = 0; i < ActiveParticles; i++)
            {
                Point screenLocation = Render.LevelToScreen(AllParticles[i].Location);
                double sizeh = Render.Scale * AllParticles[i].SizeH;
                double sizev = Render.Scale * AllParticles[i].SizeV;

                dc.DrawEllipse(new SolidColorBrush(AllParticles[i].ParticleColor), null, screenLocation, sizev, sizeh);
            }
        }


        void RemoveParticle(int location)
        {
            ActiveParticles--;
            if(ActiveParticles != location)
            {
                AllParticles[location] = AllParticles[ActiveParticles];
            }
        }
        void AddParticle(ref Particle p)
        {
            if(ActiveParticles < MaxParticles)
            {
                AllParticles[ActiveParticles++] = p;
            }
        }

        void ProcessEmitter(ParticleEmitter e, double time)
        {
            switch(e.Type)
            {
                case ParticleType.Smoke:
                    ProcessEmitter(e, time, ref SmokeEmitter);
                    break;
                case ParticleType.BeamStart:
                    ProcessEmitter(e, time, ref BeamStartEmitter);
                    break;
                case ParticleType.BeamReflect:
                    ProcessEmitter(e, time, ref BeamReflectEmitter);
                    break;
                case ParticleType.HitTarget:
                    ProcessEmitter(e, time, ref BeamTargetEmitter);
                    break;
            }
        }

        void ProcessEmitter(ParticleEmitter e, double time, ref EmitterProperties ep)
        {
            double timePerParticle = 1 / ep.Rate;
            e.Time += time;
            while(e.Time > timePerParticle)
            {
                e.Time -= timePerParticle;

                double lifespan = ep.MinLifespan + r.NextDouble() * (ep.MaxLifespan - ep.MinLifespan);
                Vector direction;
                Particle p = new Particle();
                p.Age = 0;
                p.Lifespan = lifespan;
                p.Type = ep.Behavior;
                p.Location = e.Location;
                p.Phase = r.NextDouble();
                p.Emitter = e;

                if (ep.Spread > 0)
                {
                    direction = new Vector(r.NextDouble()-0.5, r.NextDouble()-0.5);
                    direction.Normalize();
                    double distance = r.NextDouble() * ep.Spread;
                    p.Location += direction* distance;
                }

                if(ep.MaxVelocity > 0)
                {
                    direction = new Vector(r.NextDouble()-0.5, r.NextDouble()-0.5);
                    double velocity = ep.MinVelocity + r.NextDouble() * (ep.MaxVelocity - ep.MinVelocity);
                    p.Velocity = direction * velocity;
                }

                AddParticle(ref p);
            }
        }


        interface IParticleBehavior
        {
            void Update(ref Particle p);
        }

        struct EmitterProperties
        {
            public double MinVelocity;
            public double MaxVelocity;
            public double Spread;
            public double MinLifespan;
            public double MaxLifespan;
            public double Rate; // Particles per second
            public IParticleBehavior Behavior;
        }

        EmitterProperties SmokeEmitter = new EmitterProperties()
        {
            MinVelocity = 0,
            MaxVelocity = 0,
            Spread = 0.4,
            MinLifespan = 2,
            MaxLifespan = 6,
            Rate = 6,
            Behavior = new SmokeBehavior()
        };

        EmitterProperties BeamStartEmitter = new EmitterProperties()
        {
            MinVelocity = 0,
            MaxVelocity = 0,
            Spread = 0,
            MinLifespan = 3,
            MaxLifespan = 8,
            Rate = 6,
            Behavior = new BeamStartBehavior()
        };

        EmitterProperties BeamReflectEmitter = new EmitterProperties()
        {
            MinVelocity = 0,
            MaxVelocity = 0,
            Spread = 0,
            MinLifespan = 0.4,
            MaxLifespan = 0.6,
            Rate = 40,
            Behavior = new BeamReflectBehavior()
        };

        EmitterProperties BeamTargetEmitter = new EmitterProperties()
        {
            MinVelocity = 1.2,
            MaxVelocity = 2,
            Spread = 0.05,
            MinLifespan = 0.4,
            MaxLifespan = 0.6,
            Rate = 40,
            Behavior = new BeamTargetBehavior()
        };



        class SmokeBehavior : IParticleBehavior
        {
            public void Update(ref Particle p)
            {
                double life = p.Age / p.Lifespan;
                byte alpha = (byte)(Math.Sin(life * Math.PI) * 65);

                p.Velocity = new Vector(Math.Cos(p.Age * Math.PI*3), Math.Sin(p.Age * Math.PI*2)) * 0.2;
                p.SizeH = 0.15;
                p.SizeV = 0.15;

                p.ParticleColor = Color.FromArgb(alpha, 32, 25, 19);
            }
        }

        class BeamStartBehavior : IParticleBehavior
        {
            public void Update(ref Particle p)
            {
                p.SizeH = (0.5 + Math.Sin(p.Age * 8 + p.Phase * 34) * 0.4) * p.Emitter.Value;
                p.SizeV = (0.5 + Math.Sin(p.Age * 7 + p.Phase * 21) * 0.4) * p.Emitter.Value;

                double dx = Math.Cos(p.Age * 4.2 + p.Phase * 14) * p.Emitter.Value;
                double dy = Math.Cos(p.Age * 3.9 + p.Phase * 47) * p.Emitter.Value;
                p.Location = p.Emitter.Location + new Vector(dx, dy);

                byte alpha = 255;
                if (p.Age < 1) alpha = (byte)(p.Age * 255);
                if (p.Age + 1 > p.Lifespan) alpha = (byte)((p.Lifespan - p.Age) * 255);
                p.ParticleColor = Color.FromArgb(alpha, LevelRender.BeamColor.R, LevelRender.BeamColor.G, LevelRender.BeamColor.B);


            }
        }

        class BeamReflectBehavior : IParticleBehavior
        {
            public void Update(ref Particle p)
            {
                p.SizeH = p.SizeV = 0.15;

                double distance = p.Age * 0.6;

                double dx = Math.Cos(p.Phase * Math.PI * 2);
                double dy = Math.Sin(p.Phase * Math.PI * 2);

                p.Location = p.Emitter.Location + new Vector(dx, dy) * distance;

                byte alpha = 255;
                if (p.Age + 0.2 > p.Lifespan) alpha = (byte)((p.Lifespan - p.Age) * 5 * 255);
                p.ParticleColor = Color.FromArgb(alpha, LevelRender.BeamColor.R, LevelRender.BeamColor.G, LevelRender.BeamColor.B);
            }
        }

        class BeamTargetBehavior : IParticleBehavior
        {
            public void Update(ref Particle p)
            {
                double life = p.Age / p.Lifespan;
                p.SizeH = p.SizeV = Math.Sin(life*Math.PI) * 0.05;

                p.ParticleColor = Color.FromArgb(255, 255, 0, 0);
            }
        }

    }
}
