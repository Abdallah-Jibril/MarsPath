using System;
using System.Diagnostics;

namespace MarsPath
{
    /// <summary>Audio-thread jitter buffer with rebuffering and short click-free boundaries.</summary>
    internal sealed class MarsVoicePlayback
    {
        internal const int SampleRate = 48000;
        private const int PrefillSamples = SampleRate * 140 / 1000;
        private readonly float[] samples = new float[SampleRate];
        private readonly object gate = new object();
        private int read, write, buffered, fadeIn;
        private bool priming = true;
        private float lastSample;
        private double lastPacket = -10;
        private long decodedSamples, playedSamples;
        private int largestRead;
        private float inputPeak, outputPeak;
        private float receiveGainScale = 1;
        internal int Underflows { get; private set; }
        internal int Overflows { get; private set; }
        internal int BufferedSamples { get { lock (gate) return buffered; } }
        internal static double Clock => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        internal bool Receiving { get { lock (gate) return buffered > 0 || Clock - lastPacket < .40; } }

        internal void Write(byte[] payload, int offset, int count, bool headset)
        {
            lock (gate)
            {
                lastPacket = Clock;
                float framePeak = 0;
                for (int i = 0; i < count; i++)
                {
                    short value = unchecked((short)(payload[offset + i * 2] | payload[offset + i * 2 + 1] << 8));
                    framePeak = Math.Max(framePeak, Math.Abs(value / 32768f));
                }
                // Preserve quiet speech gain; strong frames need less amplification
                // so sustained vowels do not sit against the soft peak limiter.
                float targetGain = Math.Min(1, .78f / Math.Max(.0001f, framePeak * (headset ? 3.6f : 2.3f)));
                receiveGainScale = targetGain < receiveGainScale ? targetGain : Math.Min(targetGain, receiveGainScale + .008f);
                if (buffered + count > samples.Length)
                {
                    int skip = buffered + count - samples.Length;
                    read = (read + skip) % samples.Length;
                    buffered -= skip;
                    Overflows++;
                }
                for (int i = 0; i < count; i++)
                {
                    short value = unchecked((short)(payload[offset + i * 2] | payload[offset + i * 2 + 1] << 8));
                    samples[write] = Amplify((short)(value * receiveGainScale), headset);
                    inputPeak = Math.Max(inputPeak, Math.Abs(value / 32768f));
                    write = (write + 1) % samples.Length;
                }
                buffered += count;
                decodedSamples += count;
            }
        }

        internal void Read(float[] output)
        {
            lock (gate)
            {
                Array.Clear(output, 0, output.Length);
                largestRead = Math.Max(largestRead, output.Length);
                if (priming)
                {
                    // Drain a short final syllable after the sender releases talk.
                    if (buffered < Math.Max(PrefillSamples, output.Length) &&
                        (buffered == 0 || Clock - lastPacket < .18)) return;
                    priming = false;
                    fadeIn = 96;
                }
                int available = Math.Min(buffered, output.Length);
                for (int i = 0; i < available; i++)
                {
                    float sample = samples[read];
                    read = (read + 1) % samples.Length;
                    if (fadeIn > 0) sample *= 1 - fadeIn-- / 96f;
                    output[i] = sample;
                    outputPeak = Math.Max(outputPeak, Math.Abs(sample));
                }
                buffered -= available;
                playedSamples += available;
                lastSample = available == 0 ? lastSample : output[available - 1];
                if (available < output.Length)
                {
                    int fade = Math.Min(96, output.Length - available);
                    for (int i = 0; i < fade; i++) output[available + i] = lastSample * (1 - (i + 1f) / fade);
                    if (Clock - lastPacket < .12) Underflows++;
                    priming = true;
                    lastSample = 0;
                }
            }
        }

        internal static float Amplify(short sample, bool headset)
        {
            const float knee = .70f, headroom = .20f;
            float boosted = sample * ((headset ? 3.6f : 2.3f) / 32768f);
            float excess = Math.Abs(boosted) - knee;
            if (excess <= 0) return boosted;
            float limited = knee + headroom * excess / (headroom + excess);
            return boosted < 0 ? -limited : limited;
        }

        internal string Diagnostics()
        {
            lock (gate) return $"decoded={decodedSamples}, played={playedSamples}, buffered={buffered}, " +
                $"rxPeak={inputPeak:0.0000}, outputPeak={outputPeak:0.0000}, quantum={largestRead}, underruns={Underflows}";
        }

        internal void Clear()
        {
            lock (gate)
            {
                read = write = buffered = fadeIn = 0;
                priming = true;
                lastPacket = -10;
                lastSample = 0;
                receiveGainScale = 1;
            }
        }
    }
}
