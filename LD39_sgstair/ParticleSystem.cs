using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace LD39_sgstair
{


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
            foreach (ParticleEmitter e in Emitters)
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
            if (ActiveParticles != location)
            {
                AllParticles[location] = AllParticles[ActiveParticles];
            }
        }
        void AddParticle(ref Particle p)
        {
            if (ActiveParticles < MaxParticles)
            {
                AllParticles[ActiveParticles++] = p;
            }
        }

        void ProcessEmitter(ParticleEmitter e, double time)
        {
            switch (e.Type)
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
            while (e.Time > timePerParticle)
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
                    direction = new Vector(r.NextDouble() - 0.5, r.NextDouble() - 0.5);
                    direction.Normalize();
                    double distance = r.NextDouble() * ep.Spread;
                    p.Location += direction * distance;
                }

                if (ep.MaxVelocity > 0)
                {
                    direction = new Vector(r.NextDouble() - 0.5, r.NextDouble() - 0.5);
                    direction.Normalize();
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
            MinLifespan = 0.5,
            MaxLifespan = 1,
            Rate = 35,
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

                p.Velocity = new Vector(Math.Cos(p.Age * Math.PI * 3), Math.Sin(p.Age * Math.PI * 2)) * 0.2;
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
                if (p.Age < 0.1) alpha = (byte)(p.Age * 2550);
                if (p.Age + 0.1 > p.Lifespan) alpha = (byte)((p.Lifespan - p.Age) * 2550);
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
                p.SizeH = p.SizeV = Math.Sin(life * Math.PI) * 0.05;

                p.ParticleColor = Color.FromArgb(255, 255, 0, 0);
            }
        }

    }
}
