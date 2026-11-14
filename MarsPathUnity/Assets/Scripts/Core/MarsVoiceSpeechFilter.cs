using System;

namespace MarsPath
{
    /// <summary>Speech-band cleanup for the native 48 kHz Quest media output.</summary>
    internal sealed class MarsVoiceSpeechFilter
    {
        private Biquad highPass = new Biquad(90, true);
        private Biquad lowPass = new Biquad(5500, false);

        internal void Process(float[] samples)
        {
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)lowPass.Process(highPass.Process(samples[i]));
        }

        private struct Biquad
        {
            private readonly double b0, b1, b2, a1, a2;
            private double z1, z2;

            internal Biquad(double cutoff, bool highPass)
            {
                double omega = 2 * Math.PI * cutoff / MarsVoicePlayback.SampleRate;
                double cosine = Math.Cos(omega), alpha = Math.Sin(omega) / Math.Sqrt(2);
                double inverse = 1 / (1 + alpha);
                b0 = (highPass ? 1 + cosine : 1 - cosine) * .5 * inverse;
                b1 = (highPass ? -(1 + cosine) : 1 - cosine) * inverse;
                b2 = b0;
                a1 = -2 * cosine * inverse;
                a2 = (1 - alpha) * inverse;
                z1 = z2 = 0;
            }

            internal double Process(double input)
            {
                double output = b0 * input + z1;
                z1 = b1 * input - a1 * output + z2;
                z2 = b2 * input - a2 * output;
                return output;
            }
        }
    }
}
