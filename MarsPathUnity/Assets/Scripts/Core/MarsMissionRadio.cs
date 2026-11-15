using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MarsPath
{
    /// <summary>Paired Mission Control link over Wi-Fi or USB.</summary>
    internal sealed partial class MarsMissionRadio : MonoBehaviour
    {
        private const int DesktopUsbPort = 42739;
        private const int QuestVoicePort = 42741;
        private const string KeyFile = "mission-radio.key";
        private const int SampleRate = 48000;
        private const int FrameSamples = 960;
        private const int PcmBytes = FrameSamples * 2;
        private const int PayloadBytes = PcmBytes + 16;
        private const int DataOffset = 1;
        private readonly byte[] outgoingPayload = new byte[PayloadBytes];
        private byte[] incomingPayload;
        private readonly MarsVoicePlayback playback = new MarsVoicePlayback();
        private TcpListener listener;
        private TcpClient peer;
        private Connection pendingUsb, pendingWifi;
        private MarsRadioTransport transport;
        private MarsRadioDiscovery discovery;
        private IPAddress wifiAddress;
        private byte[] key;
        private string keyPath;
        private bool headset, active;
        private volatile bool voiceEnabled = true, captureActive;
        private int captureInterrupted;
        private bool peerUsesUsb, usbReady;
        private float nextUsbAttempt, nextWifiAttempt, nextKeyCheck, nextNetworkAttempt, wifiFoundAt;
        private AudioClip voiceClip;
        private AudioSource voice;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private readonly MarsRadioUsbBridge bridge = new MarsRadioUsbBridge();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject wifiDiscoveryLock;
#endif
        private AudioClip microphone;
        private string microphoneDevice;
        private float[] capture;
        private int capturePosition, captureFrameSamples;
        private bool talkRequested, waitForTalkRelease = true;
        private float[] outputMono;
        private int sentVoiceFrames, lastLoggedSent, lastLoggedReceived;
        private int skippedCaptureFrames;
        private float capturePeak, nextAudioDiagnostic;
        private volatile bool nativePlaybackActive;
#if UNITY_ANDROID && !UNITY_EDITOR
        private MarsQuestVoiceOutput questOutput;
        private bool nativeOutputUnavailable;
#endif

        internal bool Connected => transport?.Connected == true;
        internal bool CanReachQuest => Connected || usbReady || pendingWifi != null ||
            wifiAddress != null && Time.unscaledTime - wifiFoundAt <= 4f;
        internal bool Talking => microphone != null;
        internal bool TalkRequested => talkRequested;
        internal bool Receiving => active && playback.Receiving;
        internal string MicrophoneStatus { get; private set; }
        internal string LinkStatus => Connected ? (peerUsesUsb ? "USB READY" : "WI-FI READY")
            : usbReady ? "CONNECTING VIA USB" : pendingWifi != null ? "CONNECTING VIA WI-FI"
            : headset && key == null ? "PAIR VIA USB ONCE" : "LOOKING FOR RADIO";

        internal void Initialize(bool onHeadset)
        {
            headset = onHeadset;
            keyPath = Path.Combine(Application.persistentDataPath, KeyFile);
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
            voice.dopplerLevel = 0;
            voice.bypassReverbZones = true;
            voice.reverbZoneMix = 0;
            voice.spatialize = false;
            voice.spatializePostEffects = false;
            voice.bypassListenerEffects = true;
            voice.loop = true;
            voice.volume = 1f;
            voice.priority = 0;
            // A silent source clocks the native DSP filter. PCM is supplied at
            // the final mixer callback on both Windows and Android.
            voiceClip = AudioClip.Create("Mission Control radio clock", SampleRate, 1, SampleRate, false);
            voice.clip = voiceClip;
            gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 115;
            gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 6000;
        }

        internal void Tick(bool voicePage, bool focused, bool talkHeld)
        {
            float now = Time.unscaledTime;
            CheckDispatchTimeout(now);
            RefreshKey(now);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!headset && key != null)
            {
                bridge.Poll(now, keyPath, QuestVoicePort, DesktopUsbPort);
                usbReady = bridge.Ready;
            }
#endif
            if (headset && !focused)
            {
                if (active) StopRadio();
                return;
            }
            try
            {
                active = true;
                if (voiceEnabled && !voicePage) ClearPlayback();
                voiceEnabled = voicePage;
                UpdateTalkInput(focused && voicePage, talkHeld);
                if (key == null) return;
                if (transport != null && !Connected) ClosePeer();
                CompleteConnection(ref pendingUsb, true, now);
                CompleteConnection(ref pendingWifi, false, now);
                PollNetwork(now);
                if (!Connected) return;
                Receive();
                if (!Connected) return;
                if (Interlocked.Exchange(ref captureInterrupted, 0) != 0) DisarmMicrophone();
                StartVoicePlayback();
                if (talkRequested && !Talking && !Receiving && MicrophoneStatus == null) StartMicrophone();
                if (Talking) Transmit();
                SendControlFrames();
                LogVoiceDiagnostics(now);
            }
            catch (SocketException) { ClosePeer(); }
            catch (IOException) { ClosePeer(); }
            catch (ObjectDisposedException) { ClosePeer(); }
        }

        internal void SuspendQuestRadio()
        {
            if (headset) StopRadio();
        }

        private void RefreshKey(float now)
        {
            if (now < nextKeyCheck || !headset && key != null) return;
            nextKeyCheck = now + 1.5f;
            try
            {
                byte[] stored;
                if (File.Exists(keyPath))
                {
                    if (new FileInfo(keyPath).Length != MarsRadioPairing.KeyLength) return;
                    stored = File.ReadAllBytes(keyPath);
                }
                else
                {
                    if (headset) return;
                    stored = new byte[MarsRadioPairing.KeyLength];
                    using (var random = RandomNumberGenerator.Create()) random.GetBytes(stored);
                    using var file = new FileStream(keyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    file.Write(stored, 0, stored.Length);
                }
                if (stored.Length != MarsRadioPairing.KeyLength || key != null && MarsRadioPairing.Equal(key, stored, 0))
                    return;
                if (key != null) StopRadio();
                key = stored;
                if (headset) Debug.Log("MARSPATH_RADIO: paired Mission Control key available");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void UpdateTalkInput(bool focused, bool held)
        {
            if (!focused)
            {
                DisarmMicrophone();
                return;
            }
            if (!held)
            {
                waitForTalkRelease = false;
                talkRequested = false;
                StopMicrophone();
                return;
            }
            if (waitForTalkRelease || talkRequested) return;
            talkRequested = true;
            MicrophoneStatus = null;
        }

        private void StartMicrophone()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                DisarmMicrophone();
                MicrophoneStatus = "ALLOW MICROPHONE ACCESS";
                Permission.RequestUserPermission(Permission.Microphone);
                return;
            }
#endif
            try
            {
                if (Microphone.devices.Length == 0)
                {
                    DisarmMicrophone();
                    MicrophoneStatus = "NO MIC DETECTED";
                    return;
                }
                microphoneDevice = SelectMicrophone(Microphone.devices, headset);
                if (microphoneDevice == null)
                {
                    DisarmMicrophone();
                    MicrophoneStatus = "USE A PHYSICAL MIC";
                    return;
                }
                ClearPlayback();
                Microphone.GetDeviceCaps(microphoneDevice, out int minimum, out int maximum);
                int rate = maximum > 0 ? Mathf.Clamp(SampleRate, minimum, maximum) : SampleRate;
                microphone = Microphone.Start(microphoneDevice, true, 1, rate);
                if (microphone == null)
                {
                    DisarmMicrophone();
                    MicrophoneStatus = "MIC UNAVAILABLE";
                    return;
                }
                captureFrameSamples = Mathf.Max(1, Mathf.RoundToInt(microphone.frequency / 50f));
                capture = new float[captureFrameSamples * microphone.channels];
                capturePosition = 0;
                captureActive = true;
                Debug.Log($"MARSPATH_RADIO: capture device={microphoneDevice}, rate={microphone.frequency}, channels={microphone.channels}");
            }
            catch (UnityException)
            {
                DisarmMicrophone();
                MicrophoneStatus = "MIC UNAVAILABLE";
            }
        }

        internal static string SelectMicrophone(string[] devices, bool onHeadset)
        {
            if (devices.Length == 0) return null;
            if (onHeadset) return devices[0];
            // Quest Link can make its virtual microphone the Windows default,
            // even when Mission Control should capture the laptop microphone.
            foreach (string device in devices)
            {
                string name = device.ToLowerInvariant();
                if (name.Contains("oculus") || name.Contains("meta quest") || name.Contains("virtual") ||
                    name.Contains("steam") || name.Contains("loopback") || name.Contains("stereo mix") ||
                    name.Contains("wave out mix") || name.Contains("what u hear")) continue;
                return device;
            }
            return null;
        }

        private void PollNetwork(float now)
        {
            if (now >= nextNetworkAttempt)
            {
                nextNetworkAttempt = now + 2f;
                try
                {
                    if (headset) AcquireDiscoveryLock();
                    discovery ??= new MarsRadioDiscovery(key, headset);
                    if (headset && listener == null)
                    {
                        var candidate = new TcpListener(IPAddress.Any, QuestVoicePort);
                        candidate.Start(4);
                        listener = candidate;
                    }
                }
                catch (SocketException) { }
            }
            try
            {
                bool seekWifi = !Connected && pendingUsb == null && pendingWifi == null;
                IPAddress found = discovery?.Poll(now, seekWifi);
                if (found != null)
                {
                    wifiAddress = found;
                    wifiFoundAt = now;
                }
            }
            catch (SocketException)
            {
                discovery?.Dispose();
                discovery = null;
            }

            if (headset)
            {
                if (listener == null || !listener.Pending()) return;
                TcpClient client = listener.AcceptTcpClient();
                bool viaUsb = IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint).Address);
                if (viaUsb ? pendingUsb != null || peerUsesUsb : pendingWifi != null || Connected)
                    client.Close();
                else if (viaUsb) pendingUsb = new Connection(client, null, QuestVoicePort, key, true, now);
                else pendingWifi = new Connection(client, null, QuestVoicePort, key, true, now);
                return;
            }

            if (usbReady && !peerUsesUsb && pendingUsb == null && now >= nextUsbAttempt)
            {
                nextUsbAttempt = now + 1f;
                pendingUsb = new Connection(new TcpClient(AddressFamily.InterNetwork), IPAddress.Loopback, DesktopUsbPort, key, false, now);
            }
            if (Connected || pendingUsb != null || pendingWifi != null || wifiAddress == null ||
                now - wifiFoundAt > 4f || now < nextWifiAttempt) return;
            nextWifiAttempt = now + 1.5f;
            pendingWifi = new Connection(new TcpClient(AddressFamily.InterNetwork), wifiAddress, QuestVoicePort, key, false, now);
        }

        private void AcquireDiscoveryLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (wifiDiscoveryLock != null) return;
            AndroidJavaObject pending = null;
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var wifi = context.Call<AndroidJavaObject>("getSystemService", "wifi");
                if (wifi == null) return;
                pending = wifi.Call<AndroidJavaObject>("createMulticastLock", "MarsPath discovery");
                pending.Call("setReferenceCounted", false);
                pending.Call("acquire");
                wifiDiscoveryLock = pending;
                pending = null;
            }
            catch (AndroidJavaException) { }
            finally { pending?.Dispose(); }
#endif
        }

        private void ReleaseDiscoveryLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (wifiDiscoveryLock == null) return;
            try { wifiDiscoveryLock.Call("release"); }
            catch (AndroidJavaException) { }
            wifiDiscoveryLock.Dispose();
            wifiDiscoveryLock = null;
#endif
        }

        private void CompleteConnection(ref Connection pending, bool viaUsb, float now)
        {
            if (pending == null) return;
            if (pending.Handshake.Status == TaskStatus.RanToCompletion && (!Connected || viaUsb && !peerUsesUsb) &&
                (headset || !viaUsb || usbReady))
            {
                ClosePeer();
                OpenTransport(pending.Client, pending.Handshake.Result, viaUsb);
                pending = null;
                if (viaUsb) CancelConnection(ref pendingWifi);
                Debug.Log("MARSPATH_RADIO: " + (viaUsb ? "USB ready" : "Wi-Fi ready"));
            }
            else if (pending.Handshake.IsCompleted || now >= pending.Deadline)
            {
                CancelConnection(ref pending);
                if (viaUsb) nextUsbAttempt = now + 1.5f;
            }
        }

        internal void OpenTransport(TcpClient client, MarsRadioPairing authenticated, bool viaUsb)
        {
            peer = client;
            peerUsesUsb = viaUsb;
            playback.ConfigureLatency(viaUsb);
            transport = new MarsRadioTransport(client, authenticated, PayloadBytes,
                (byte)MessageKind.LiveState, ReceiveVoice);
        }

        private sealed class Connection
        {
            internal readonly TcpClient Client;
            internal readonly Task<MarsRadioPairing> Handshake;
            internal readonly float Deadline;

            internal Connection(TcpClient client, IPAddress address, int port, byte[] key, bool headset, float now)
            {
                Client = client;
                Deadline = now + 5f;
                Handshake = Task.Run(async () =>
                {
                    if (address != null) await client.ConnectAsync(address, port).ConfigureAwait(false);
                    client.NoDelay = true;
                    return MarsRadioPairing.Authenticate(client, key, headset, PayloadBytes);
                });
            }
        }

        private void Transmit()
        {
            int position = Microphone.GetPosition(microphoneDevice);
            if (position < 0) return;
            int available = (position - capturePosition + microphone.samples) % microphone.samples;
            const int maximumCaptureFrames = 6;
            if (available > captureFrameSamples * maximumCaptureFrames)
            {
                skippedCaptureFrames += available / captureFrameSamples - maximumCaptureFrames;
                capturePosition = (position - captureFrameSamples * maximumCaptureFrames + microphone.samples) % microphone.samples;
                available = captureFrameSamples * maximumCaptureFrames;
            }
            while (available >= captureFrameSamples && Connected)
            {
                if (!microphone.GetData(capture, capturePosition)) break;
                EncodeVoice(microphone.channels);
                WriteFrame();
                capturePosition = (capturePosition + captureFrameSamples) % microphone.samples;
                available -= captureFrameSamples;
            }
        }

        private void EncodeVoice(int channels)
        {
            BeginFrame(MessageKind.Voice);
            float step = captureFrameSamples / (float)FrameSamples;
            for (int i = 0; i < FrameSamples; i++)
            {
                float source = i * step;
                int first = (int)source;
                int next = Mathf.Min(first + 1, captureFrameSamples - 1);
                float sum = 0;
                for (int channel = 0; channel < channels; channel++)
                    sum += Mathf.LerpUnclamped(capture[first * channels + channel], capture[next * channels + channel], source - first);
                int pcm = Mathf.RoundToInt(Mathf.Clamp(sum / channels, -1f, 1f) * short.MaxValue);
                capturePeak = Mathf.Max(capturePeak, Mathf.Abs(sum / channels));
                outgoingPayload[DataOffset + i * 2] = (byte)pcm;
                outgoingPayload[DataOffset + i * 2 + 1] = (byte)(pcm >> 8);
            }
            sentVoiceFrames++;
        }

        internal void Receive()
        {
            for (int i = 0; i < 64 && transport.TryReceive(out incomingPayload); i++)
            {
                if (!ReceiveControlFrame()) { ClosePeer(); return; }
            }
        }

        private void ReceiveVoice(byte[] payload)
        {
            // Runs on the network thread. Unity microphone calls stay in Tick.
            if (!voiceEnabled || !headset && captureActive) return;
            if (headset && captureActive) Interlocked.Exchange(ref captureInterrupted, 1);
            playback.Write(payload, DataOffset, FrameSamples, headset);
        }

        private void StartVoicePlayback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (headset && voiceEnabled && !nativeOutputUnavailable)
            {
                try
                {
                    // A native output and the Unity DSP callback must never
                    // consume or play the same receive stream concurrently.
                    nativePlaybackActive = true;
                    if (voice.isPlaying) voice.Stop();
                    questOutput ??= new MarsQuestVoiceOutput(playback);
                    if (questOutput.Failure != null) throw new InvalidOperationException(questOutput.Failure);
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("MARSPATH_VOICE: media output fallback: " + exception.Message);
                    questOutput?.Dispose(); questOutput = null;
                    nativePlaybackActive = false;
                    nativeOutputUnavailable = true;
                }
            }
#endif
            if (voiceEnabled && !voice.isPlaying) voice.Play();
        }

        private static float AmplifyVoice(short sample, bool onHeadset)
        {
            return MarsVoicePlayback.Amplify(sample, onHeadset);
        }

        [UnityEngine.Scripting.Preserve]
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (nativePlaybackActive)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            if (channels == 1) { playback.Read(data); return; }
            int frames = data.Length / channels;
            if (outputMono == null || outputMono.Length != frames) outputMono = new float[frames];
            playback.Read(outputMono);
            for (int frame = 0; frame < frames; frame++)
                for (int channel = 0; channel < channels; channel++)
                    data[frame * channels + channel] = outputMono[frame];
        }

        private void LogVoiceDiagnostics(float now)
        {
            if (now < nextAudioDiagnostic || sentVoiceFrames == lastLoggedSent &&
                transport.ReceivedVoiceFrames == lastLoggedReceived) return;
            nextAudioDiagnostic = now + 2;
            lastLoggedSent = sentVoiceFrames;
            lastLoggedReceived = transport.ReceivedVoiceFrames;
            string output = "Unity DSP";
#if UNITY_ANDROID && !UNITY_EDITOR
            if (questOutput != null) output = "Android media, " + questOutput.Diagnostics;
#endif
            Debug.Log($"MARSPATH_VOICE: {(headset ? "Quest" : "Laptop")}, sent={sentVoiceFrames}, " +
                $"micPeak={capturePeak:0.0000}, received={lastLoggedReceived}, playing={voice.isPlaying}, " +
                $"sink={output}, outputRate={AudioSettings.outputSampleRate}, captureSkipped={skippedCaptureFrames}, " +
                $"sendDropped={transport.DroppedVoiceFrames}, {playback.Diagnostics()}");
        }

        private void StopMicrophone()
        {
            captureActive = false;
            if (microphone == null) return;
            Microphone.End(microphoneDevice);
            Destroy(microphone);
            microphone = null;
            capture = null;
        }

        private void DisarmMicrophone()
        {
            talkRequested = false;
            waitForTalkRelease = true;
            StopMicrophone();
        }

        private void ClearPlayback()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            questOutput?.Dispose(); questOutput = null;
#endif
            nativePlaybackActive = false;
            if (voice != null) voice.Stop();
            playback.Clear();
        }

        private void ClosePeer()
        {
            StopMicrophone();
            transport?.Dispose();
            transport = null;
            peer?.Close();
            peer = null;
            peerUsesUsb = false;
            ResetControlConnection();
            ClearPlayback();
        }

        private static void CancelConnection(ref Connection pending)
        {
            if (pending == null) return;
            pending.Client.Close();
            _ = pending.Handshake.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion) task.Result?.Dispose();
                else _ = task.Exception;
            }, TaskScheduler.Default);
            pending = null;
        }

        private void StopRadio()
        {
            active = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            questOutput?.Dispose(); questOutput = null;
#endif
            DisarmMicrophone();
            listener?.Stop();
            listener = null;
            discovery?.Dispose();
            discovery = null;
            ReleaseDiscoveryLock();
            CancelConnection(ref pendingUsb);
            CancelConnection(ref pendingWifi);
            ClosePeer();
            wifiAddress = null;
            nextUsbAttempt = nextWifiAttempt = nextNetworkAttempt = 0;
        }

        private void OnDisable() => StopRadio();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) DisarmMicrophone();
        }
        private void OnApplicationPause(bool paused)
        {
            if (paused) DisarmMicrophone();
        }
        private void OnDestroy()
        {
            if (voiceClip != null) Destroy(voiceClip);
        }
    }
}
