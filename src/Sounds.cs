using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace MuteMyMic
{
    // Two short tones generated in code: falling = muted, rising = unmuted.
    // Played with PlaySound(SND_MEMORY | SND_ASYNC) from PINNED buffers: Windows keeps reading the
    // buffer after the call returns, so it must not be moved by the garbage collector
    // (SoundPlayer only pins it for the duration of the call). UI thread only.
    static class Sounds
    {
        const uint SND_ASYNC = 0x1, SND_NODEFAULT = 0x2, SND_MEMORY = 0x4;

        static GCHandle mutedWav, liveWav;
        static int buffersVolume = -1;

        /// <param name="volume">0..100</param>
        public static void Play(bool muted, int volume)
        {
            try
            {
                if (volume != buffersVolume)
                {
                    Stop();  // never free a buffer that may still be playing
                    Free();
                    double amplitude = 0.4 * Math.Max(0, Math.Min(100, volume)) / 100.0;
                    mutedWav = GCHandle.Alloc(MakeWav(amplitude, 880, 587.33), GCHandleType.Pinned);
                    liveWav = GCHandle.Alloc(MakeWav(amplitude, 587.33, 880), GCHandleType.Pinned);
                    buffersVolume = volume;
                }
                GCHandle wav = muted ? mutedWav : liveWav;
                PlaySound(wav.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
            }
            catch (Exception ex)
            {
                Log.Write(ex); // no audio output etc. — the sound is only a convenience
            }
        }

        /// <summary>Stops any sound and releases the buffers (on exit).</summary>
        public static void Shutdown()
        {
            Stop();
            Free();
            buffersVolume = -1;
        }

        static void Stop()
        {
            try { PlaySound(IntPtr.Zero, IntPtr.Zero, 0); } catch { }
        }

        static void Free()
        {
            if (mutedWav.IsAllocated) mutedWav.Free();
            if (liveWav.IsAllocated) liveWav.Free();
        }

        static byte[] MakeWav(double volume, params double[] freqs)
        {
            const int rate = 44100;
            var samples = new List<short>();
            foreach (double f in freqs)
            {
                int n = rate * 70 / 1000;
                for (int i = 0; i < n; i++)
                {
                    double attack = Math.Min(1.0, i / (rate * 0.005));
                    double release = Math.Min(1.0, (n - i) / (rate * 0.02));
                    double v = Math.Sin(2 * Math.PI * f * i / rate) * attack * release * volume;
                    samples.Add((short)(v * short.MaxValue));
                }
                for (int i = 0; i < rate * 15 / 1000; i++) samples.Add(0);
            }

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataBytes = samples.Count * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);       // PCM
                w.Write((short)1);       // mono
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (short s in samples) w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }

        [DllImport("winmm.dll", SetLastError = true)]
        static extern bool PlaySound(IntPtr sound, IntPtr hmod, uint flags);
    }
}
