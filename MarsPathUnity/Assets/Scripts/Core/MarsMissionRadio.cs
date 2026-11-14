using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MarsPath
{
    /// <summary>Two-way push-to-talk with a paired Quest over Wi-Fi or USB.</summary>
    internal sealed class MarsMissionRadio : MonoBehaviour
    {
        private const int DesktopUsbPort = 42739;
        private const int QuestVoicePort = 42741;
        private const string KeyFile = "mission-radio.key";
        private const int SampleRate = 48000;
        private const int FrameSamples = 960;
        private const int PcmBytes = FrameSamples * 2;
        private const float PlaybackGain = 2.3f;
        private readonly byte[] outgoingPcm = new byte[PcmBytes], incomingPcm = new byte[PcmBytes];
        private readonly byte[] outgoingFrame = new byte[MarsRadioPairing.FrameLength(PcmBytes)];
        private readonly byte[] incomingFrame = new byte[MarsRadioPairing.FrameLength(PcmBytes)];
        private readonly float[] playback = new float[SampleRate / 4];
        private TcpListener listener;
        private TcpClient peer;
        private Connection pendingUsb, pendingWifi;
        private MarsRadioPairing pairing;
        private MarsRadioDiscovery discovery;
        private IPAddress wifiAddress;
        private byte[] key;
        private string keyPath;
        private bool headset, active;
        private bool peerUsesUsb, usbReady;
        private float lastVoice = -10f, nextUsbAttempt, nextWifiAttempt, nextKeyCheck, nextNetworkAttempt, wifiFoundAt;
        private AudioClip voiceClip;
        private AudioSource voice;
        private int frameOffset, readPosition, writePosition, buffered;
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

        internal bool Connected => peer != null && peer.Connected;
        internal bool Talking => microphone != null;
        internal bool TalkRequested => talkRequested;
        internal bool Receiving => active && Time.unscaledTime - lastVoice < .35f;
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
            voice.loop = true;
            voice.volume = 1f;
            voice.priority = 0;
            voiceClip = AudioClip.Create("Mission Control radio", SampleRate, 1, SampleRate, true, ReadVoice);
            voice.clip = voiceClip;
            gameObject.AddComponent<AudioHighPassFilter>().cutoffFrequency = 165;
            gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 4500;
        }

        internal void Tick(bool inMission, bool focused, bool talkHeld)
        {
            float now = Time.unscaledTime;
            RefreshKey(now);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!headset && key != null)
            {
                bridge.Poll(now, keyPath, QuestVoicePort, DesktopUsbPort);
                usbReady = bridge.Ready;
            }
#endif
            if (!inMission || headset && !focused)
            {
                if (active) StopRadio();
                return;
            }
            try
            {
                active = true;
                UpdateTalkInput(focused, talkHeld);
                if (key == null) return;
                if (!headset && !usbReady && peerUsesUsb) ClosePeer();
                CompleteConnection(ref pendingUsb, true, now);
                CompleteConnection(ref pendingWifi, false, now);
                PollNetwork(now);
                if (!Connected) return;
                if (talkRequested && !Talking && MicrophoneStatus == null) StartMicrophone();
                NetworkStream stream = peer.GetStream();
                Receive(stream);
                if (Connected && Talking) Transmit(stream);
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                DisarmMicrophone();
                MicrophoneStatus = "ALLOW MICROPHONE ACCESS";
                Permission.RequestUserPermission(Permission.Microphone);
            }
#endif
        }

        private void StartMicrophone()
        {
            try
            {
                if (Microphone.devices.Length == 0)
                {
                    DisarmMicrophone();
                    MicrophoneStatus = "NO MIC DETECTED";
                    return;
                }
                microphoneDevice = SelectMicrophone(Microphone.devices, headset);
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
                    name.Contains("steam") || name.Contains("loopback") || name.Contains("stereo mix")) continue;
                return device;
            }
            return devices[0];
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
                peer = pending.Client;
                pairing = pending.Handshake.Result;
                peerUsesUsb = viaUsb;
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
                    client.SendBufferSize = 8192;
                    return MarsRadioPairing.Authenticate(client, key, headset, PcmBytes);
                });
            }
        }

        private void Transmit(NetworkStream stream)
        {
            int position = Microphone.GetPosition(microphoneDevice);
            if (position < 0) return;
            int available = (position - capturePosition + microphone.samples) % microphone.samples;
            if (available > captureFrameSamples * 6)
            {
                capturePosition = (position - captureFrameSamples * 3 + microphone.samples) % microphone.samples;
                available = captureFrameSamples * 3;
            }
            while (available >= captureFrameSamples && peer.Client.Poll(0, SelectMode.SelectWrite))
            {
                if (!microphone.GetData(capture, capturePosition)) break;
                EncodeVoice(microphone.channels);
                pairing.Encrypt(outgoingPcm, outgoingFrame);
                stream.Write(outgoingFrame, 0, outgoingFrame.Length);
                capturePosition = (capturePosition + captureFrameSamples) % microphone.samples;
                available -= captureFrameSamples;
            }
        }

        private void EncodeVoice(int channels)
        {
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
                outgoingPcm[i * 2] = (byte)pcm;
                outgoingPcm[i * 2 + 1] = (byte)(pcm >> 8);
            }
        }

        private void Receive(NetworkStream stream)
        {
            for (int i = 0; i < 8 && stream.DataAvailable; i++)
            {
                int read = stream.Read(incomingFrame, frameOffset, incomingFrame.Length - frameOffset);
                if (read == 0) { ClosePeer(); return; }
                frameOffset += read;
                if (frameOffset == incomingFrame.Length)
                {
                    if (!pairing.TryDecrypt(incomingFrame, incomingPcm)) { ClosePeer(); return; }
                    // Receiving must also work while the local talk button is held.
                    QueueVoice();
                    frameOffset = 0;
                }
            }
            if (peer.Client.Poll(0, SelectMode.SelectRead) && peer.Available == 0) ClosePeer();
        }

        private void QueueVoice()
        {
            bool enoughToPlay;
            lock (playback)
            {
                int overflow = Mathf.Max(0, buffered + FrameSamples - playback.Length);
                readPosition = (readPosition + overflow) % playback.Length;
                buffered -= overflow;
                for (int i = 0; i < FrameSamples; i++)
                {
                    short sample = unchecked((short)(incomingPcm[i * 2] | incomingPcm[i * 2 + 1] << 8));
                    playback[writePosition] = AmplifyVoice(sample);
                    writePosition = (writePosition + 1) % playback.Length;
                }
                buffered += FrameSamples;
                enoughToPlay = buffered >= 2 * FrameSamples;
            }
            lastVoice = Time.unscaledTime;
            if (!voice.isPlaying && enoughToPlay) voice.Play();
        }

        private static float AmplifyVoice(short sample)
        {
            const float knee = .70f, headroom = .20f;
            float boosted = sample * (PlaybackGain / 32768f);
            float excess = Math.Abs(boosted) - knee;
            if (excess <= 0f) return boosted;

            // Compress loud peaks gradually while leaving quieter speech at full gain.
            float limited = knee + headroom * excess / (headroom + excess);
            return boosted < 0f ? -limited : limited;
        }

        private void ReadVoice(float[] data)
        {
            lock (playback)
            {
                int available = Math.Min(buffered, data.Length);
                for (int i = 0; i < available; i++)
                {
                    data[i] = playback[readPosition];
                    readPosition = (readPosition + 1) % playback.Length;
                }
                buffered -= available;
                Array.Clear(data, available, data.Length - available);
            }
        }

        private void StopMicrophone()
        {
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
            if (voice != null) voice.Stop();
            lastVoice = -10f;
            lock (playback) readPosition = writePosition = buffered = 0;
        }

        private void ClosePeer()
        {
            StopMicrophone();
            peer?.Close();
            peer = null;
            peerUsesUsb = false;
            pairing?.Dispose();
            pairing = null;
            frameOffset = 0;
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
