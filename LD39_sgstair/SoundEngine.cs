using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LD39_sgstair
{
    class SoundEngine : IAudioProducer
    {
        const double FadeLength = 0.05; // In seconds
        const double ReverbDelay = 0.05;
        const double ReverbAmount = 0.1;

        DSAudioOut AudioOut;
        public SoundEngine(IntPtr windowHandle)
        {
            // Prepare a sample buffer
            int buffersize = 44100 * 200 / 1000; // Max theoretical buffer requirement should be 100ms with current parameters.
            ReverbSampleCount = (int)Math.Round(44100 * ReverbDelay);
            RenderBuffer = new double[buffersize * 2];

            AudioOut = new DSAudioOut(AudioFormat.Stereo_44k_16, windowHandle);
            AudioOut.AddProducer(this);
        }
        double SampleRate = 44100;
        int ReverbSampleCount;

        double[] RenderBuffer;

        public void SoftReset()
        {
            // Mark all the existing sound sources as exiting.
            lock (SourceLock)
            {
                foreach (SoundSource s in Sources)
                {
                    s.Stopping = true;
                    s.Fade = FadeLength;
                }
            }
        }

        object SourceLock = new object();
        List<SoundSource> Sources = new List<SoundSource>();

        Random r = new Random();


        public void AddSource(SoundSource s)
        {
            lock (SourceLock)
            {
                s.Stopping = false;
                s.Fade = 0;
                Sources.Add(s);
            }
        }
        public void RemoveSource(SoundSource s)
        {
            lock (SourceLock)
            {
                s.Stopping = true;
                s.Fade = FadeLength;
            }
        }


        public void RenderSamples(float[] SampleBuffer, int renderCount, AudioFormat format)
        {
            // Rely on there being 2 channels for simplicity.
            // In each pair of floats, Left channel is first, then Right channel.

            // Accumulate sources into buffer
            Array.Clear(RenderBuffer, ReverbSampleCount * 2, renderCount * 2);

            List<SoundSource> RemoveSources = new List<SoundSource>();
            lock (SourceLock)
            {
                foreach (SoundSource s in Sources)
                {
                    // Render in ~2ms timesteps for better granularity on the volume envelopes.
                    int cursor = 0;
                    while (cursor < renderCount)
                    {
                        int renderLength = renderCount - cursor;
                        if (renderLength > 100) renderLength = 100;

                        switch (s.Type)
                        {
                            case SoundType.Laser:
                                RenderLaser(s, RenderBuffer, cursor + ReverbSampleCount, renderLength);
                                break;

                            case SoundType.Target:
                                RenderTarget(s, RenderBuffer, cursor + ReverbSampleCount, renderLength);
                                break;

                            case SoundType.Rotate:
                                RenderRotate(s, RenderBuffer, cursor + ReverbSampleCount, renderLength);
                                break;

                            case SoundType.Wall:
                                RenderWall(s, RenderBuffer, cursor + ReverbSampleCount, renderLength);
                                break;
                        }

                        double time = (double)renderLength / SampleRate;
                        s.Age += time;
                        if (s.Stopping) s.Fade -= time; else s.Fade += time;
                        cursor += renderLength;
                    }
                    if (s.Stopping && s.Fade <= 0) { RemoveSources.Add(s); }
                }

                foreach (SoundSource s in RemoveSources)
                {
                    Sources.Remove(s);
                }
            }

            // compute reverb
            // Reverb waveform is inverted, and stereo swapped
            for (int i = 0; i < renderCount; i++)
            {
                RenderBuffer[(ReverbSampleCount + i) * 2] -= RenderBuffer[i * 2 + 1] * ReverbAmount;
                RenderBuffer[(ReverbSampleCount + i) * 2 + 1] -= RenderBuffer[i * 2] * ReverbAmount;
            }

            // Emit the sound
            for (int i = 0; i < renderCount * 2; i++)
            {
                SampleBuffer[i] = (float)RenderBuffer[i + ReverbSampleCount * 2];
            }


            // Move the end of the audio data into the computed reverb location for the next time.
            Array.Copy(RenderBuffer, renderCount * 2, RenderBuffer, 0, ReverbSampleCount * 2);
        }

        void ComputeVolumes(SoundSource s, out double left, out double right)
        {
            double fadeVol = 0;
            if (s.Fade > FadeLength) fadeVol = 1;
            else if (s.Fade > 0) fadeVol = s.Fade / FadeLength;
            if (fadeVol > 0)
            {
                left = s.Volume * fadeVol * Math.Cos((s.Pan + 1) * Math.PI / 4);
                right = s.Volume * fadeVol * Math.Sin((s.Pan + 1) * Math.PI / 4);
            }
            else
            {
                left = right = 0;
            }
        }

        void AccumulateSaw(double[] buffer, int firstIndex, int sampleCount, double frequency, double left, double right, ref double valuestorage)
        {
            double rate = frequency / SampleRate;
            for (int i = 0; i < sampleCount; i++)
            {

                double value = valuestorage * 2 - 1;
                buffer[(firstIndex + i) * 2] += value * left;
                buffer[(firstIndex + i) * 2 + 1] += value * right;

                valuestorage += rate;
                if (valuestorage > 1) valuestorage -= 1;
            }
        }

        void AccumulateSquare(double[] buffer, int firstIndex, int sampleCount, double frequency, double left, double right, ref double valuestorage)
        {
            double rate = frequency / SampleRate;
            for (int i = 0; i < sampleCount; i++)
            {

                double value = valuestorage > 0.5 ? 1 : -1;
                buffer[(firstIndex + i) * 2] += value * left;
                buffer[(firstIndex + i) * 2 + 1] += value * right;

                valuestorage += rate;
                if (valuestorage > 1) valuestorage -= 1;
            }
        }

        void AccumulateFilterNoise(double[] buffer, int firstIndex, int sampleCount, double frequency, double filter, double left, double right, ref double valuestorage1, ref double valuestorage2, ref double valuestorage3)
        {
            double rate = frequency / SampleRate;

            for (int i = 0; i < sampleCount; i++)
            {

                double value = valuestorage2;
                buffer[(firstIndex + i) * 2] += value * left;
                buffer[(firstIndex + i) * 2 + 1] += value * right;

                valuestorage1 += rate;
                if (valuestorage1 > 1)
                {
                    valuestorage1 -= 1;
                    valuestorage2 = r.NextDouble() * 2 - 1;
                }
            }
        }

        void RenderLaser(SoundSource s, double[] AccumulateBuffer, int firstIndex, int sampleCount)
        {
            double volLeft, volRight;
            ComputeVolumes(s, out volLeft, out volRight);
            if (volLeft == 0 && volRight == 0) return;

            double volVary = 1 + Math.Sin(s.Age * Math.PI * 3) * 0.05 + Math.Sin(s.Age * Math.PI * 11) * 0.1;

            AccumulateSaw(AccumulateBuffer, firstIndex, sampleCount, s.Frequency, volLeft * volVary, volRight * volVary, ref s.Value1);

            /*
            double f = s.Frequency * (0.8 + (Math.Sin(s.Age * Math.PI / 3) + 1) * 0.5);
            AccumulateSaw(AccumulateBuffer, firstIndex, sampleCount, f, volLeft / 5, volRight / 5, ref s.Value2);

            f = s.Frequency * (0.8 + (Math.Cos(s.Age * Math.PI / 2) + 1) * 0.5);
            AccumulateSaw(AccumulateBuffer, firstIndex, sampleCount, f, volLeft / 5, volRight / 5, ref s.Value3);
            */
        }

        void RenderTarget(SoundSource s, double[] AccumulateBuffer, int firstIndex, int sampleCount)
        {
            double volLeft, volRight;
            ComputeVolumes(s, out volLeft, out volRight);
            if (volLeft == 0 && volRight == 0) return;

            double volMod = Math.Abs(Math.Sin(s.Age * Math.PI * 2 * 8));
            AccumulateSquare(AccumulateBuffer, firstIndex, sampleCount, s.Frequency, volLeft * volMod, volRight * volMod, ref s.Value1);

        }

        void RenderRotate(SoundSource s, double[] AccumulateBuffer, int firstIndex, int sampleCount)
        {
            double volLeft, volRight;
            ComputeVolumes(s, out volLeft, out volRight);
            if (volLeft == 0 && volRight == 0) return;
            AccumulateSquare(AccumulateBuffer, firstIndex, sampleCount, s.Frequency, volLeft, volRight, ref s.Value1);
        }

        void RenderWall(SoundSource s, double[] AccumulateBuffer, int firstIndex, int sampleCount)
        {
            double volLeft, volRight;
            ComputeVolumes(s, out volLeft, out volRight);
            if (volLeft == 0 && volRight == 0) return;


            double filter = 1000;
            double freq = s.Frequency;
            double mod = Math.Min(s.Age, 5)/5;
            //freq = freq * (1 - (0.5 * mod));
            AccumulateFilterNoise(AccumulateBuffer, firstIndex, sampleCount, freq, filter, volLeft, volRight, ref s.Value1, ref s.Value2, ref s.Value3);
        }
    }

    enum SoundType
    {
        Laser,
        Target,
        Rotate,
        Wall
    }

    class SoundSource
    {
        /// <summary>
        /// 0-1 volume
        /// </summary>
        public double Volume = 0.15;

        /// <summary>
        /// -1..1 pan (-1 = left, 1=right)
        /// </summary>
        public double Pan = 0;


        /// <summary>
        /// Variable that counts up with time, for making effects that change with time in a static fashion.
        /// </summary>
        public double Age = 0;

        /// <summary>
        /// How long this souce has been playing - Use this to taper the audio in and out
        /// </summary>
        public double Fade = 0;

        /// <summary>
        /// Reverse the Fade variable direction and stop the audio after it reaches zero
        /// </summary>
        public bool Stopping;


        /// <summary>
        /// Core frequency of the sound
        /// </summary>
        public double Frequency = 440;

        /// <summary>
        /// Parameter that can be specified;
        /// </summary>
        public double Param1;


        /// <summary>
        /// How the sound is generated
        /// </summary>
        public SoundType Type;


        // Some internal tracking values
        public double Value1, Value2, Value3, Value4, Value5, Value6;
    }

}
