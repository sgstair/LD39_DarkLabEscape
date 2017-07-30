using SharpDX;
using SharpDX.DirectSound;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LD39_sgstair
{

    public interface IAudioOut
    {
        AudioFormat Format { get; }
        void AddProducer(IAudioProducer newProducer);
        void RemoveProducer(IAudioProducer removeProducer);

    }

    public interface IAudioProducer
    {
        /// <summary>
        /// AudioProducer must render renderCount samples - Each sample is one float per channel.
        /// AudioProducer is responsible for controlling its own master volume.
        /// </summary>
        void RenderSamples(float[] SampleBuffer, int renderCount, AudioFormat format);
    }

    public class AudioFormat
    {
        public AudioFormat(int sampleRate, int numChannels, int bits) { SampleRate = sampleRate; Channels = numChannels; BitsPerSample = bits; }
        public readonly int SampleRate;
        public readonly int Channels;
        public readonly int BitsPerSample;
        public int BytesPerSample { get { return (BitsPerSample + 7) / 8; } }

        public static AudioFormat Mono_44k_16 { get { return new AudioFormat(44100, 1, 16); } }
        public static AudioFormat Stereo_44k_16 { get { return new AudioFormat(44100, 2, 16); } }
        public static AudioFormat Mono_48k_16 { get { return new AudioFormat(48000, 1, 16); } }
        public static AudioFormat Stereo_48k_16 { get { return new AudioFormat(48000, 2, 16); } }
    }

    public class DSAudioOut : AudioOutCommon
    {
        const double buffer_ms = 50;

        public DSAudioOut(AudioFormat fmt, IntPtr windowHandle) : base(fmt)
        {
            if (fmt.BitsPerSample != 16) { throw new ArgumentOutOfRangeException("fmt.BitsPerSample", "DSAudioOut only supports 16-bits per sample currently."); }
            sampleBuffer = new short[MaxRenderSamples * fmt.Channels];
            Setup(fmt, windowHandle);
            Start();
        }

        DirectSound ds;
        SecondarySoundBuffer buf;
        Thread fillerThread;
        int lastCursor;
        int BufferBytes;
        int BufferSamples;
        int MinSamples;
        int BytesPerSample;

        short[] sampleBuffer;

        void Setup(AudioFormat fmt, IntPtr wndHandle)
        {
            BufferSamples = (int)Math.Ceiling(fmt.SampleRate * buffer_ms / 1000.0);
            MinSamples = (BufferSamples + 3) / 4;
            BytesPerSample = fmt.BytesPerSample * fmt.Channels;
            BufferBytes = BufferSamples * BytesPerSample;

            ds = new DirectSound();
            ds.SetCooperativeLevel(wndHandle, CooperativeLevel.Normal);


            WaveFormat wf = new WaveFormat(fmt.SampleRate, fmt.BitsPerSample, fmt.Channels);

            SoundBufferDescription desc = new SoundBufferDescription();
            desc.AlgorithmFor3D = Guid.Empty;
            desc.Flags = BufferFlags.GlobalFocus | BufferFlags.ControlPositionNotify;
            desc.Format = wf;
            desc.BufferBytes = BufferBytes; // about 100ms

            buf = new SecondarySoundBuffer(ds, desc);

            // Zero fill
            DataStream bufData, bufData2;
            bufData = buf.Lock(0, buf.Capabilities.BufferBytes, LockFlags.EntireBuffer, out bufData2);

            bufData.WriteRange(new byte[buf.Capabilities.BufferBytes], 0, buf.Capabilities.BufferBytes);

            buf.Unlock(bufData, bufData2);
        }
        void Start()
        {
            fillerThread = new Thread(FillThread);
            fillerThread.IsBackground = true;
            fillerThread.Priority = ThreadPriority.Highest;
            fillerThread.Start();
            lastCursor = 0;

            buf.Play(0, PlayFlags.Looping);
        }

        public void Stop()
        {
            buf.Stop();
        }


        void FillThread(object o)
        {
            while (true)
            {
                int cursor, writeCursor;
                buf.GetCurrentPosition(out cursor, out writeCursor);
                // Ignore write cursor.
                int bytesNeeded = (cursor + BufferBytes - lastCursor) % BufferBytes;
                int samplesNeeded = bytesNeeded / BytesPerSample;

                while (samplesNeeded > MinSamples)
                {
                    int thisSamples = samplesNeeded;

                    // Generate samples from audio producers
                    thisSamples = ProduceSamples(thisSamples);

                    // convert incoming sample data into target bit size
                    for (int i = 0; i < thisSamples * Format.Channels; i++)
                    {
                        sampleBuffer[i] = (short)(Math.Max(-1.0f, Math.Min(1.0f, MixBuffer[i])) * 32767);
                    }


                    // Dump samples into buffer.

                    DataStream bufData, bufData2;
                    bufData = buf.Lock(lastCursor, thisSamples * BytesPerSample, 0, out bufData2);

                    bufData.WriteRange(sampleBuffer, 0, (int)bufData.Length / 2);
                    if (bufData.Length < thisSamples * BytesPerSample)
                    {
                        bufData2.WriteRange(sampleBuffer, (int)bufData.Length / 2, (int)bufData2.Length / 2);
                    }
                    buf.Unlock(bufData, bufData2);

                    lastCursor = (lastCursor + thisSamples * BytesPerSample) % BufferBytes;
                    samplesNeeded -= thisSamples;
                }

                Thread.Sleep(0);
            }
        }

    }


    public class AudioOutCommon : IAudioOut
    {
        protected const int MaxRenderSamples = 4096;
        public AudioOutCommon(AudioFormat fmt)
        {
            Format = fmt;
            AudioProducers = new List<IAudioProducer>();
            MixBuffer = new float[MaxRenderSamples * fmt.Channels];
            TempBuffer = new float[MaxRenderSamples * fmt.Channels];
        }
        public AudioFormat Format { get; private set; }
        public void AddProducer(IAudioProducer newProducer)
        {
            lock (AudioProducers)
            {
                AudioProducers.Add(newProducer);
            }
        }
        public void RemoveProducer(IAudioProducer removeProducer)
        {
            lock (AudioProducers)
            {
                AudioProducers.Remove(removeProducer);
            }
        }

        List<IAudioProducer> AudioProducers;

        protected float[] MixBuffer;
        float[] TempBuffer;

        /// <summary>
        /// Produces and mixes incoming samples. Returns actual number of samples produced.
        /// Output is in MixBuffer.
        /// </summary>
        public int ProduceSamples(int sampleCount)
        {
            if (sampleCount > MaxRenderSamples)
            {
                sampleCount = MaxRenderSamples;
            }
            if (sampleCount == 0) { throw new Exception("Unexpected: ProduceSamples called with 0 samples."); }

            Array.Clear(MixBuffer, 0, sampleCount * Format.Channels);
            IAudioProducer[] producers = null;
            lock (AudioProducers)
            {
                producers = AudioProducers.ToArray();
            }

            if (producers.Length == 1)
            {
                producers[0].RenderSamples(MixBuffer, sampleCount, Format);
            }
            else
            {
                foreach (IAudioProducer prod in producers)
                {
                    Array.Clear(TempBuffer, 0, sampleCount * Format.Channels);
                    prod.RenderSamples(TempBuffer, sampleCount, Format);
                    for (int i = 0; i < sampleCount * Format.Channels; i++)
                    {
                        MixBuffer[i] += TempBuffer[i];
                    }
                }
            }
            return sampleCount;
        }
    }


}
