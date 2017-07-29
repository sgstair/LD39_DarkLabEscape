using System;
using System.Collections.Generic;
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
        const double TargetSize = 0.1;

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

        public int LevelIndex = -1;


        public Rect LevelArea
        {
            get
            {
                var xs = ActiveFeatures.Select(f => f.p1.X).Concat(ActiveFeatures.Select(f => f.p2.X));
                var ys = ActiveFeatures.Select(f => f.p1.Y).Concat(ActiveFeatures.Select(f => f.p2.Y));
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

        public virtual Vector SurfaceNormal(Point intersectionPoint)
        {
            return lineNormal; // Normal is always the same.
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
