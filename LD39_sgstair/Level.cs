using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LD39_sgstair
{
    /// <summary>
    /// Level encapsulates the details about a specific level, and provides utility methods to assist in computing in the level.
    /// </summary>
    class Level
    {
        const double TargetSize = 0.13;
        const double LaserSpeed = 2;

        /// <summary>
        /// Location that the laser beam starts from (may be offset due to graphics)
        /// </summary>
        public Point LaserLocation;

        /// <summary>
        /// Location of the target to complete the level
        /// </summary>
        public Point TargetLocation;

        /// <summary>
        /// Initial direction that the laser is pointing.
        /// </summary>
        public Vector LaserInitialDirection;

        /// <summary>
        /// List of walls, reflectors, lens elements, etc.
        /// </summary>
        public List<LevelFeature> ActiveFeatures = new List<LevelFeature>();


        public List<LevelDecoration> Decorations = new List<LevelDecoration>();

        public int LevelIndex = -1;


        /// <summary>
        /// Current laser heading
        /// </summary>
        public double LaserAngle;

        /// <summary>
        /// Laser rotational speed
        /// </summary>
        public double LaserAngleSpeed;

        public double DesiredAngle;

        /// <summary>
        /// Power applied to the target
        /// </summary>
        public double TargetPower = 0;
        public bool AppliedTargetPower;

        public double LaserLength = 0;


        public double TargetPowerDecay = 2;

        const double DrivePower = 0.6;
        const double InertiaDrag = 0.7;
        const double SnapDistance = 0.002;

        public void StartLevel()
        {
            LaserAngle = 0;
            if(LaserInitialDirection.LengthSquared != 0)
            {
                LaserAngle = Math.Atan2(-LaserInitialDirection.Y, LaserInitialDirection.X);
            }
            TargetPower = 0;
            LaserLength = 0;
        }

        public void UpdateLevel(double time, bool laserOn)
        {
            double difference = DesiredOffset();

            double drag = Math.Pow(InertiaDrag, time);
            LaserAngleSpeed = LaserAngleSpeed * drag + Math.Sign(difference) * time * DrivePower;

            // Cheat and limit the speed as a function of distance.
            double maxSpeed = Math.Abs(difference * 5);
            LaserAngleSpeed = Math.Min(maxSpeed, LaserAngleSpeed);
            LaserAngleSpeed = Math.Max(-maxSpeed, LaserAngleSpeed);

            LaserAngle += LaserAngleSpeed * time;
            if(Math.Abs(LaserAngle-DesiredAngle) < SnapDistance)
            {
                LaserAngle = DesiredAngle;
                LaserAngleSpeed *= 0.2;
            }

            if(laserOn)
            {
                LaserLength += time * LaserSpeed;
            }
            else
            {
                LaserLength = 0;
            }

            if(!AppliedTargetPower)
            {
                TargetPower = Math.Max(0, TargetPower - TargetPowerDecay * time);
            }
            else
            {
                TargetPower = TargetPower + time;
            }
            AppliedTargetPower = false;
        }

        double DesiredOffset()
        {
            double a = DesiredAngle - LaserAngle;
            while (a < Math.PI) a += Math.PI * 2;
            while (a > Math.PI) a -= Math.PI * 2;
            return a;
        }

        public void SetDesiredVector(Vector v)
        {
            DesiredAngle = Math.Atan2(-v.Y, v.X);
        }


        public void SaveLevel(string filename)
        {
            List<string> levelData = new List<string>();
            levelData.Add(SaveElement("LaserLocation", LaserLocation));
            levelData.Add(SaveElement("TargetLocation", TargetLocation));
            levelData.Add(SaveElement("LaserInitialDirection", LaserInitialDirection));
            levelData.Add(SaveElement("ActiveFeatures", ActiveFeatures.Count));
            foreach(var f in ActiveFeatures)
            {
                levelData.Add(f.SaveData());
            }
            levelData.Add(SaveElement("Decorations", Decorations.Count));
            foreach (var d in Decorations)
            {
                levelData.Add(d.SaveData());
            }
            File.WriteAllLines(filename, levelData);
        }

        public static Level LoadLevel(string filename)
        {
            string[] lines = File.ReadAllLines(filename);
            return LoadLevel(lines);
        }

        public static Level LoadLevel(string[] lines)
        {
            int cursor = 0;
            Level l = new Level();
            while(cursor < lines.Length)
            {
                string[] split = lines[cursor].Split('|');
                cursor++;
                switch(split[0])
                {
                    case "LaserLocation":
                        LoadElement(split, out l.LaserLocation);
                        break;
                    case "TargetLocation":
                        LoadElement(split, out l.TargetLocation);
                        break;
                    case "LaserInitialDirection":
                        LoadElement(split, out l.LaserInitialDirection);
                        break;
                    case "ActiveFeatures":
                        {
                            int count;
                            LoadElement(split, out count);
                            for (int i = 0; i < count; i++)
                            {
                                l.ActiveFeatures.Add(LevelFeature.LoadData(lines[cursor++]));
                            }
                        }
                        break;
                    case "Decorations":
                        {
                            int count;
                            LoadElement(split, out count);
                            for (int i = 0; i < count; i++)
                            {
                                l.Decorations.Add(LevelDecoration.LoadData(lines[cursor++]));
                            }
                        }
                        break;

                    default:
                        throw new Exception("Testing: Should not have unexpected elements in file!");
                }
            }
            return l;
        }

        string SaveElement(string name, object arg)
        {
            if (arg is Point)
            {
                Point p = (Point)arg;
                return $"{name}|{p.X:R}|{p.Y:R}";
            }
            if(arg is Vector)
            {
                Vector v = (Vector)arg;
                return $"{name}|{v.X:R}|{v.Y:R}";
            }
            if(arg is int)
            {
                return $"{name}|{(int)arg}";
            }
            throw new Exception($"Unable to save element {name}");
        }
        static void LoadElement(string[] pieces, out Point p)
        {
            if (pieces.Length < 3) throw new ArgumentOutOfRangeException();
            p = new Point(double.Parse(pieces[1]), double.Parse(pieces[2]));
        }
        static void LoadElement(string[] pieces, out Vector v)
        {
            if (pieces.Length < 3) throw new ArgumentOutOfRangeException();
            v = new Vector(double.Parse(pieces[1]), double.Parse(pieces[2]));
        }
        static void LoadElement(string[] pieces, out int i)
        {
            if (pieces.Length < 2) throw new ArgumentOutOfRangeException();
            i = int.Parse(pieces[1]);
        }




        public Rect LevelArea
        {
            get
            {
                var xs = ActiveFeatures.Select(f => f.p1.X).Concat(ActiveFeatures.Select(f => f.p2.X));
                var ys = ActiveFeatures.Select(f => f.p1.Y).Concat(ActiveFeatures.Select(f => f.p2.Y));
                if (xs.Count() == 0) return new Rect(0, 0, 1, 1);
                return new Rect(new Point(xs.Min(), ys.Min()), new Point(xs.Max(), ys.Max()));
            }
        }

        public Ray GenerateRayFromAngle(double angle)
        {
            Vector v = new Vector(Math.Cos(angle), -Math.Sin(angle));
            return new Ray() { Origin = LaserLocation, Direction = v };
        }

        public Ray GenerateRayFromPoint(Point p)
        {
            Vector v = p - LaserLocation;
            v.Normalize();
            return new Ray() { Origin = LaserLocation, Direction = v };
        }


        public RayPath TracePath(Ray startingRay, double traceDistance)
        {
            RayPath p = new RayPath() { LinkedLevel = this };
            p.TracedDistance = 0;
            p.Interactions.Add(new RayInteraction() { InteractionFeature = null, RayOut = startingRay });
            p.Trace(traceDistance);
            return p;
        }

        public Point? RayHitTarget(Ray startingRay)
        {
            // Find nearest point on the ray to the target location.
            Vector rayNormal = new Vector(startingRay.Direction.Y, -startingRay.Direction.X);
            rayNormal.Normalize();

            double distanceFromRay = (startingRay.Origin - TargetLocation).Dot(rayNormal);
            // We only hit the target if the ray's nearest point is within the TargetSize radius
            if(Math.Abs(distanceFromRay) <= TargetSize)
            {
                // Nearest point on the ray to the target
                Point nearestPoint = TargetLocation - rayNormal * distanceFromRay;

                // Ensure that this point is in front of the ray, not behind.
                if ((nearestPoint - startingRay.Origin).Dot(startingRay.Direction) < 0)
                    return null;

                // Back off the point to the first intersection with the target, in case this makes a difference (it will in some cases)
                double intersectAngle = Math.Acos(Math.Abs(distanceFromRay) / TargetSize);
                double adjustAmount = Math.Sin(intersectAngle) * TargetSize;
                nearestPoint -= startingRay.Direction * adjustAmount;
                return nearestPoint;
            }
            return null;
        }

        public RayInteraction InteractRay(Ray startingRay)
        {
            LevelFeature featureInteraction = null;
            Point interactPoint = new Point();
            double interactMinDistanceSquared = 0;
            
            foreach(LevelFeature f in ActiveFeatures)
            {
                Point? p = f.NearestIntersectionPoint(startingRay);
                if(p != null)
                {
                    double distanceSquared = (startingRay.Origin - p.Value).LengthSquared;
                    if(featureInteraction == null || distanceSquared < interactMinDistanceSquared)
                    {
                        interactPoint = p.Value;
                        featureInteraction = f;
                        interactMinDistanceSquared = distanceSquared;
                    }
                }
            }
            // Check if ray hit the target
            Point? intersectTarget = RayHitTarget(startingRay);
            if(intersectTarget != null)
            {
                double targetDistanceSquared = (intersectTarget.Value - startingRay.Origin).LengthSquared;
                if(featureInteraction == null || targetDistanceSquared < interactMinDistanceSquared)
                {
                    // Next interaction point is actually the target. Target absorbs the ray.
                    RayInteraction ri = new RayInteraction();
                    startingRay.Length = Math.Sqrt(targetDistanceSquared);
                    ri.RayIn = startingRay;
                    ri.HitTarget = true;
                    return ri;
                }
            }


            if(featureInteraction != null)
            {
                RayInteraction ri = new RayInteraction();
                startingRay.Length = Math.Sqrt(interactMinDistanceSquared);
                ri.RayIn = startingRay;
                ri.InteractionFeature = featureInteraction;

                // If the properties are right, generate a new ray
                if(featureInteraction.WillReflect)
                {
                    // Generate reflected ray
                    Ray reflected = new Ray() { Origin = interactPoint, PreviousDistance = startingRay.PreviousDistance + startingRay.Length };
                    Vector n = featureInteraction.SurfaceNormal(interactPoint);
                    Vector rv = startingRay.Direction;
                    rv += n * (-n.Dot(rv) * 2);
                    reflected.Direction = rv;
                    reflected.Advance(0.0001); // Advance the ray a tiny amount to prevent it from reflecting on the same wall again.
                                                // This could potentially make it possible to run rays through corners though, think about this some more.
                    ri.RayOut = reflected;
                }
                else
                {
                    // Generate refracted ray
                }
                return ri;
            }
            return null;
        }

        public List<LevelRegion> IdentifyRegions()
        {
            List<LevelRegion> regionsOut = new List<LevelRegion>();
            // Untag all features
            foreach (LevelFeature f in ActiveFeatures)
            {
                f.Tag = false;

            }
            while (true)
            {
                // Find an untagged region
                LevelFeature startingFeature = null;
                foreach (LevelFeature f in ActiveFeatures)
                {
                    if (f.Tag == false)
                    {
                        startingFeature = f;
                        f.Tag = true;
                        break;
                    }
                }
                if (startingFeature == null)
                {
                    break;
                }

                // Find all loops attached to this region.
                List<RegionTrackingPath> path = new List<RegionTrackingPath>();
                RegionTrackingPath p = new RegionTrackingPath() { f = startingFeature, p = startingFeature.p1 };
                path.Add(p);
                RecursiveFindLoops(path, regionsOut);
            }

            return regionsOut;
        }

        void RecursiveFindLoops(List<RegionTrackingPath> path, List<LevelRegion> regionsOut)
        {
            // Find any loops involving the last point added to the path
            // If we find a loop, this branch is dead. Other branches will find the other loops
            RegionTrackingPath last = path.Last();
            foreach(RegionTrackingPath prev in path.Take(path.Count-1))
            {
                if(last.p == prev.p)
                {
                    // found a loop. Create a Region for it.
                    LevelRegion r = new LevelRegion();
                    r.Origin = last.p;
                    foreach(RegionTrackingPath node in path.Reverse<RegionTrackingPath>())
                    {
                        if (node.p == r.Origin && r.Perimeter.Count > 0) break;
                        r.Perimeter.Add(node.f);
                    }

                    // Exclude identical regions.
                    HashSet<LevelFeature> usedFeatures = new HashSet<LevelFeature>();
                    foreach (LevelFeature f in r.Perimeter) usedFeatures.Add(f);

                    foreach(LevelRegion lr in regionsOut)
                    {
                        if(lr.Perimeter.Count == r.Perimeter.Count)
                        {
                            bool equivalent = true;
                            foreach(LevelFeature f in lr.Perimeter)
                            {
                                if(!usedFeatures.Contains(f))
                                {
                                    equivalent = false; break;
                                }
                            }
                            if (equivalent) return; // Don't add equivalent regions
                        }
                    }

                    r.ComputeRect();
                    r.Area = r.ComputeArea();
                    regionsOut.Add(r);
                    return;
                }
            }
            

            // Find all the paths out of the last node which are not also the last feature.
            Queue<RegionTrackingPath> newPaths = new Queue<RegionTrackingPath>();
            foreach (LevelFeature f in ActiveFeatures)
            {
                if (f != last.f)
                {
                    Point? newPoint = f.Connect(last.p);
                    if(newPoint != null)
                    {
                        f.Tag = true;
                        newPaths.Enqueue(new RegionTrackingPath() { f = f, p = newPoint.Value });
                    }
                }
            }
            while(newPaths.Count > 1)
            {
                // Generate new path lists as we are split across multiple directions
                List<RegionTrackingPath> newPath = new List<RegionTrackingPath>(path);
                newPath.Add(newPaths.Dequeue());
                RecursiveFindLoops(newPath, regionsOut);
            }
            if(newPaths.Count == 1)
            {
                // Continue to use the passed path
                path.Add(newPaths.Dequeue());
                RecursiveFindLoops(path, regionsOut);
            }
        }

    }

    public class LevelDecoration
    {
        public Point p;
        public int DecorationIndex;
        public double Rotation;



        public virtual string SaveData()
        {
            return $"LevelDecoration|{p.X:R}|{p.Y:R}|{DecorationIndex}|{Rotation:R}";
        }
        public static LevelDecoration LoadData(string sourceData)
        {
            string[] split = sourceData.Split('|');
            if (split[0] != "LevelDecoration") throw new Exception("Invalid key in decoration parse");
            LevelDecoration d = new LevelDecoration();
            d.p = new Point(double.Parse(split[1]), double.Parse(split[2]));
            d.DecorationIndex = int.Parse(split[3]);
            d.Rotation = double.Parse(split[4]);
            return d;
        }
    }



    class RegionTrackingPath
    {
        public Point p;
        public LevelFeature f;
    }

    class LevelRegion
    {
        public Point Origin;
        public List<LevelFeature> Perimeter = new List<LevelFeature>();
        public double Area;
        public Rect EnclosingRect;
        public void ComputeRect()
        {
            var xs = Perimeter.Select(f => f.p1.X).Concat(Perimeter.Select(f => f.p2.X));
            var ys = Perimeter.Select(f => f.p1.Y).Concat(Perimeter.Select(f => f.p2.Y));
            if (xs.Count() == 0) { EnclosingRect = new Rect(0, 0, 0, 0); return; }
            EnclosingRect = new Rect(new Point(xs.Min(), ys.Min()), new Point(xs.Max(), ys.Max()));
        }

        public double ComputeArea()
        {
            Point cur, last;
            cur = last = new Point();
            cur = Origin;
            double totalArea = 0;
            for(int i=0;i<Perimeter.Count-1;i++)
            {
                cur = Perimeter[i].Connect(cur).Value;
                if(i>0)
                {
                    Vector dLast = (last - Origin);
                    Vector nLast = new Vector(dLast.Y, -dLast.X);
                    nLast.Normalize();
                    // compute area for this triangle.
                    double l1 = (dLast).Length;
                    double l2 = (cur - last).Dot(nLast);
                    totalArea += l1 * l2 / 2;
                }
                last = cur;

            }
            return Math.Abs(totalArea);
        }

        public Point[] ClosedForm()
        {
            List<Point> points = new List<Point>();
            points.Add(Origin);
            Point p = Origin;
            for(int i=0;i<Perimeter.Count-1;i++)
            {
                p = Perimeter[i].Connect(p).Value;
                points.Add(p);
            }
            return points.ToArray();
        }
    }


    class RefractiveIndex
    {
        public static double Air = 1;
        public static double Glass = 1.5;
        public static double Diamond = 2.42;
    }

    class Ray
    {
        public Point Origin;
        public Vector Direction;
        public double PreviousDistance;
        public double Length;

        public void Advance(double length)
        {
            Origin += Direction * length;
            PreviousDistance += length;
        }

        public override string ToString()
        {
            return $"Ray({Origin}=>{Direction} {PreviousDistance} {Length})";
        }
    }

    class RayInteraction
    {
        public Ray RayIn, RayOut;
        public LevelFeature InteractionFeature;
        public bool HitTarget;

        public override string ToString()
        {
            return $"RayInteraction(In:{RayIn} Out:{RayOut} {InteractionFeature})";
        }
    }

    class RayPath
    {
        public Level LinkedLevel;
        public List<RayInteraction> Interactions = new List<RayInteraction>();
        /// <summary>
        /// The distance to the last interaction that's been traced through. If the last interaction is the target, this is the distance to the target.
        /// </summary>
        public double TracedDistance;

        public bool HitTarget
        {
            get
            {
                return Interactions.Last().HitTarget;
            }
        }

        public void Trace(double newDistance)
        {
            while(newDistance > TracedDistance && Interactions.Last().RayOut != null)
            {
                Ray r = Interactions.Last().RayOut;
                RayInteraction ri = LinkedLevel.InteractRay(r);
                if (ri != null)
                {
                    Interactions.Add(ri);
                    TracedDistance = ri.RayIn.PreviousDistance + ri.RayIn.Length;
                }
                else
                {
                    // Nothing else to interact with.
                    break;
                }
            }
        }

        public void DbgPrint()
        {
            System.Diagnostics.Debug.WriteLine("RayPath");
            foreach(RayInteraction ri in Interactions)
            {
                System.Diagnostics.Debug.WriteLine("  " + ri.ToString());
            }
        }
    }

    public static class VectorExtensions
    {
        public static double Dot(this Vector v1, Vector v2)
        {
            return v1.X * v2.X + v1.Y * v2.Y;
        }
    }




    class LevelFeature
    {
        /// <summary>
        /// Most features are just a simple line from one point to another
        /// </summary>
        public Point p1, p2;

        /// <summary>
        /// Indices of refraction for lens calculations
        /// </summary>
        public double leftIndex = RefractiveIndex.Air, rightIndex = RefractiveIndex.Air;

        public bool WillReflect = true;
        public bool WillRefract = false;

        /// <summary>
        /// Used for feature region identification.
        /// </summary>
        public bool Tag;

        public Point? Connect(Point origin)
        {
            if (p1 == origin) return p2;
            if(p2 == origin) return p1;
            return null;
        }

        public Vector NormalFrom(Point origin)
        {
            if (p1 == origin) return lineNormal;
            if (p2 == origin) return -lineNormal;
            throw new Exception("Invalid input");
        }


        public override string ToString()
        {
            return $"LevelFeature(({p1})-({p2}))";
        }

        public virtual void ResetNormal()
        {
            savedNormal = null;
        }

        Vector? savedNormal;
        /// <summary>
        /// Line normal. If looking from p1 to p2, (where +y is down, +x is right), the normal is looking 90 degrees to the left.
        /// </summary>
        Vector lineNormal
        {
            get
            {
                if (savedNormal.HasValue) return savedNormal.Value;

                Vector v = p2 - p1;
                v.Normalize();
                savedNormal = new Vector(v.Y, -v.X);
                return savedNormal.Value;
            }
        }

        /// <summary>
        /// Intersect a ray with this element. Virtual so it can be overridden by more complex things.
        /// </summary>
        public virtual Point? NearestIntersectionPoint(Ray intersectWith)
        {
            // Does ray intersect with this feature?
            double distance = (intersectWith.Origin - p1).Dot(lineNormal); // Minimum distance of ray starting point from line (positive = left of line)
            double approachRate = -intersectWith.Direction.Dot(lineNormal); // How quickly the ray is moving towards the line
            if (approachRate == 0) return null; // Ray is parallel to this line, will never approach.
            if (Math.Sign(distance) != Math.Sign(approachRate)) return null; // Ray is going in the opposite direction to this line.

            Point intersectionPoint = intersectWith.Origin + intersectWith.Direction * (distance / approachRate);
            // is the intersection point within this line segment?
            if ((intersectionPoint - p1).Dot(p2 - p1) < 0) return null;  // Beyond p1 on that side
            if ((intersectionPoint - p2).Dot(p1 - p2) < 0) return null; // Beyond p2 on that side

            // Intersection point is inside the line segment, return this point.
            return intersectionPoint;
        }


        /// <summary>
        /// Determine how far away this point is from the element (for level editor, mainly)
        /// </summary>
        public virtual double DistanceToPoint(Point nearbyPoint)
        {
            // Does ray intersect with this feature?
            double distance = (nearbyPoint - p1).Dot(lineNormal); // Minimum distance of ray starting point from line (positive = left of line)
            Point intersectionPoint = nearbyPoint - lineNormal * distance;;
            // is the intersection point within this line segment?
            if ((intersectionPoint - p1).Dot(p2 - p1) < 0) return (nearbyPoint-p1).Length;  // Beyond p1 on that side
            if ((intersectionPoint - p2).Dot(p1 - p2) < 0) return (nearbyPoint-p2).Length; // Beyond p2 on that side

            // Intersection point is inside the line segment, return this point.
            return Math.Abs(distance);
        }


        public virtual Vector SurfaceNormal(Point intersectionPoint)
        {
            return lineNormal; // Normal is always the same.
        }

        public virtual string SaveData()
        {
            // Consider reflecting to get class name here rather than setting LevelFeature for all children that don't override SaveData.
            return $"LevelFeature|{p1.X:R}|{p1.Y:R}|{p2.X:R}|{p2.Y:R}|{leftIndex:R}|{rightIndex:R}|{WillReflect}|{WillRefract}";
        }

        public static LevelFeature LoadData(string feature)
        {
            string[] split = feature.Split('|');
            if(split[0] == "LevelFeature")
            {
                LevelFeature f = new LevelFeature();
                f.LoadCommon(split);
                return f;
            }
            else if(split[0] == "LevelEntryExitDoor")
            {
                LevelEntryExitDoor d = new LevelEntryExitDoor();
                d.LoadCommon(split);
                d.ExitDoor = bool.Parse(split[9]);
                return d;
            }
            // Future, other types
            throw new Exception($"Unable to load feature of type {split[0]}");
        }
        internal void LoadCommon(string[] split)
        {
            p1 = new Point(double.Parse(split[1]), double.Parse(split[2]));
            p2 = new Point(double.Parse(split[3]), double.Parse(split[4]));
            leftIndex = double.Parse(split[5]);
            rightIndex = double.Parse(split[6]);
            WillReflect = bool.Parse(split[7]);
            WillRefract = bool.Parse(split[8]);
            savedNormal = null;
        }
    }

    class LevelEntryExitDoor : LevelFeature
    {
        public bool ExitDoor;

        public override string SaveData()
        {
            return $"LevelEntryExitDoor|{p1.X:R}|{p1.Y:R}|{p2.X:R}|{p2.Y:R}|{leftIndex:R}|{rightIndex:R}|{WillReflect}|{WillRefract}|{ExitDoor}";
        }

    }


    class LevelGenerator
    {
        public static Level GenerateLevel(Guid levelSeed)
        {
            LevelGenerator gen = new LevelGenerator(levelSeed);
            return gen.Lvl;
        }
        Random r;
        Level Lvl = new Level();
        private LevelGenerator(Guid levelSeed)
        {
            r = new Random(levelSeed.GetHashCode());


            // For now make a really simple level, to test with.
            AddRectangle(new Rect(0, 0, 10, 8));

            Lvl.LaserLocation = new Point(1.5, 1.5);
            Lvl.TargetLocation = new Point(4.5, 2.5);
            Lvl.LaserInitialDirection = new Vector(1, 0);
        }

        void AddWall(Point p1, Point p2)
        {
            Lvl.ActiveFeatures.Add(new LevelFeature() { p1 = p1, p2 = p2 });
        }
        void AddRectangle(Rect r)
        {
            AddWall(r.TopLeft, r.TopRight);
            AddWall(r.TopRight, r.BottomRight);
            AddWall(r.BottomRight, r.BottomLeft);
            AddWall(r.BottomLeft, r.TopLeft);
        }



    }

}
