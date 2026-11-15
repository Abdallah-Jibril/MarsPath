#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MarsPath
{
    /// <summary>Quest speech uses an Android media track with its own sample clock.</summary>
    internal sealed class MarsQuestVoiceOutput : IDisposable
    {
        private readonly MarsVoicePlayback playback;
        private readonly AndroidJavaObject track;
        private readonly Task writer;
        private volatile bool disposed;
        private volatile string failure;
        private long writtenSamples;
        private int bufferFrames, outputUnderruns;
        private readonly int capacityFrames;

        internal string Failure => failure;
        internal long WrittenSamples => Interlocked.Read(ref writtenSamples);
        internal string Diagnostics => $"written={WrittenSamples}, nativeBufferMs={Volatile.Read(ref bufferFrames) * 1000 / MarsVoicePlayback.SampleRate}, " +
            $"nativeUnderruns={Volatile.Read(ref outputUnderruns)}";

        internal MarsQuestVoiceOutput(MarsVoicePlayback playback)
        {
            this.playback = playback;
            using var audio = new AndroidJavaClass("android.media.AudioTrack");
            int minimum = audio.CallStatic<int>("getMinBufferSize", MarsVoicePlayback.SampleRate, 4, 2);
            if (minimum <= 0) throw new InvalidOperationException("Quest PCM output format unavailable.");
            using var attributeBuilder = new AndroidJavaObject("android.media.AudioAttributes$Builder");
            using var usage = attributeBuilder.Call<AndroidJavaObject>("setUsage", 1); // USAGE_MEDIA
            using var speech = attributeBuilder.Call<AndroidJavaObject>("setContentType", 1); // CONTENT_TYPE_SPEECH
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                if (version.GetStatic<int>("SDK_INT") >= 32)
                {
                    using var spatial = attributeBuilder.Call<AndroidJavaObject>("setSpatializationBehavior", 1); // NEVER
                }
            }
            using var attributes = attributeBuilder.Call<AndroidJavaObject>("build");
            using var formatBuilder = new AndroidJavaObject("android.media.AudioFormat$Builder");
            using var encoding = formatBuilder.Call<AndroidJavaObject>("setEncoding", 2); // PCM_16BIT
            using var rate = formatBuilder.Call<AndroidJavaObject>("setSampleRate", MarsVoicePlayback.SampleRate);
            using var channels = formatBuilder.Call<AndroidJavaObject>("setChannelMask", 4); // CHANNEL_OUT_MONO
            using var format = formatBuilder.Call<AndroidJavaObject>("build");
            using var builder = new AndroidJavaObject("android.media.AudioTrack$Builder");
            using var trackAttributes = builder.Call<AndroidJavaObject>("setAudioAttributes", attributes);
            using var trackFormat = builder.Call<AndroidJavaObject>("setAudioFormat", format);
            using var trackBuffer = builder.Call<AndroidJavaObject>("setBufferSizeInBytes", Math.Max(minimum, 7680));
            using var streaming = builder.Call<AndroidJavaObject>("setTransferMode", 1);
            using var performance = builder.Call<AndroidJavaObject>("setPerformanceMode", 1); // LOW_LATENCY
            track = builder.Call<AndroidJavaObject>("build");
            if (track.Call<int>("getState") != 1)
            {
                track.Call("release"); track.Dispose();
                throw new InvalidOperationException("Quest media output did not initialize.");
            }
            capacityFrames = track.Call<int>("getBufferCapacityInFrames");
            int requestedFrames = Math.Max(minimum / 2, 960);
            bufferFrames = track.Call<int>("setBufferSizeInFrames", requestedFrames);
            if (bufferFrames <= 0) bufferFrames = track.Call<int>("getBufferSizeInFrames");
            track.Call<int>("setVolume", 1f);
            track.Call("play");
            writer = Task.Run(WriteAudio);
        }

        private void WriteAudio()
        {
            AndroidJNI.AttachCurrentThread();
            try
            {
                var mono = new float[960];
                var pcm = new sbyte[mono.Length * 2];
                var speechFilter = new MarsVoiceSpeechFilter();
                int blocks = 0;
                while (!disposed)
                {
                    playback.Read(mono);
                    speechFilter.Process(mono);
                    for (int i = 0; i < mono.Length; i++)
                    {
                        int sample = (int)Math.Round(Math.Max(-1, Math.Min(1, mono[i])) * 32767);
                        pcm[i * 2] = unchecked((sbyte)sample);
                        pcm[i * 2 + 1] = unchecked((sbyte)(sample >> 8));
                    }
                    int offset = 0;
                    while (offset < pcm.Length && !disposed)
                    {
                        // Blocking writes pace the buffer on the hardware audio clock,
                        // independent of the renderer and Unity voice virtualization.
                        int written = track.Call<int>("write", pcm, offset, pcm.Length - offset);
                        if (written < 0) throw new InvalidOperationException("Quest audio write failed: " + written);
                        if (written == 0) { Thread.Sleep(2); continue; }
                        offset += written;
                        Interlocked.Add(ref writtenSamples, written / 2);
                    }
                    if (++blocks % 50 == 0)
                    {
                        int underruns = track.Call<int>("getUnderrunCount");
                        if (underruns > outputUnderruns)
                        {
                            int requested = Math.Min(capacityFrames, bufferFrames + 960);
                            int actual = track.Call<int>("setBufferSizeInFrames", requested);
                            if (actual > 0) Volatile.Write(ref bufferFrames, actual);
                        }
                        Volatile.Write(ref outputUnderruns, underruns);
                    }
                }
            }
            catch (Exception exception)
            {
                if (!disposed) failure = exception.GetType().Name + ": " + exception.Message;
            }
            finally
            {
                try { track.Call("stop"); track.Call("release"); }
                catch (Exception) { }
                track.Dispose();
                AndroidJNI.DetachCurrentThread();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // A write can be blocked on a suspended device. Pause releases it.
            if (!writer.IsCompleted)
            {
                try { track.Call("pause"); track.Call("flush"); }
                catch (Exception) { }
            }
            writer.Wait(250);
        }
    }
}
#endif
