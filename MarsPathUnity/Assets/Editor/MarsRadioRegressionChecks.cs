using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MarsPath.Editor
{
    public static class MarsRadioRegressionChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int PayloadBytes = 1936;

        public static void CheckAndBuildWindows()
        {
            Run();
            MarsPathProjectSetup.BuildWindowsPrototype();
        }

        [MenuItem("MarsPath/Check Two Way Radio")]
        public static void Run()
        {
            Type radioType = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsMissionRadio", true);
            Type pairingType = radioType.Assembly.GetType("MarsPath.MarsRadioPairing", true);
            MethodInfo select = radioType.GetMethod("SelectMicrophone", BindingFlags.Static | BindingFlags.NonPublic);
            string chosen = (string)select.Invoke(null, new object[] {
                new[] { "Microphone (Oculus Virtual Audio Device)", "Microphone Array (Realtek Audio)" }, false });
            Require(chosen.Contains("Realtek"), "Laptop capture must skip the Quest virtual microphone");
            Require(select.Invoke(null, new object[] { new[] { "Stereo Mix", "Meta Quest microphone" }, false }) == null,
                "Laptop capture must never fall back to a loopback input");
            Debug.Log("MARSPATH_RADIO: available laptop microphone selection=" +
                select.Invoke(null, new object[] { Microphone.devices, false }));
            MethodInfo amplify = radioType.GetMethod("AmplifyVoice", BindingFlags.Static | BindingFlags.NonPublic);
            Require((float)amplify.Invoke(null, new object[] { (short)3276, false }) > .22f, "Desktop speech gain");
            Require((float)amplify.Invoke(null, new object[] { (short)3276, true }) > .34f, "Higher Quest speech gain");
            Require(Math.Abs((float)amplify.Invoke(null, new object[] { short.MinValue, true })) < .91f, "Quest peak limiter");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var desktop = new TcpClient();
            desktop.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using TcpClient headset = listener.AcceptTcpClient();
            listener.Stop();
            MethodInfo authenticate = pairingType.GetMethod("Authenticate", BindingFlags.Static | BindingFlags.NonPublic);
            var key = new byte[32];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
            Task<object> headsetPairing = Task.Run(() => authenticate.Invoke(null, new object[] { headset, key, true, PayloadBytes }));
            var desktopPairing = (IDisposable)authenticate.Invoke(null, new object[] { desktop, key, false, PayloadBytes });
            Require(headsetPairing.Wait(6000), "Pairing timeout");
            var questPairing = (IDisposable)headsetPairing.Result;

            var desktopRoot = new GameObject("Mission Control regression");
            var questRoot = new GameObject("Quest radio regression");
            bool previousAudioPause = AudioListener.pause;
            Component desktopRadio = desktopRoot.AddComponent(radioType);
            Component questRadio = questRoot.AddComponent(radioType);
            try
            {
                AudioListener.pause = true;
                Attach(desktopRadio, desktop, desktopPairing, false);
                Attach(questRadio, headset, questPairing, true);
                CheckDispatch(desktopRadio, questRadio, desktop, headset);
                CheckVoice(desktopRadio, questRadio, .36f, false);
                CheckVoice(questRadio, desktopRadio, .23f, false);
                Set(questRadio, "captureActive", true);
                CheckVoice(desktopRadio, questRadio, .36f, false);
                Require((int)Get(questRadio, "captureInterrupted") != 0,
                    "Incoming Mission Control voice must interrupt Quest capture on its next frame");
                Set(questRadio, "captureActive", false);
                Set(desktopRadio, "captureActive", true);
                CheckVoice(questRadio, desktopRadio, 0f, true);
                Set(desktopRadio, "captureActive", false);
                CheckJitterBuffer();
                CheckRouteFacing();
                CheckCompass();
                CheckSpeechCleanup();
            }
            finally
            {
                Detach(desktopRadio);
                Detach(questRadio);
                UnityEngine.Object.DestroyImmediate(desktopRoot);
                UnityEngine.Object.DestroyImmediate(questRoot);
                AudioListener.pause = previousAudioPause;
            }
            Debug.Log("MARSPATH_RADIO_REGRESSION_PASSED: background encrypted transport, exact route handoff, live state and ready gate, voice without render ticks, jitter buffer and half-duplex.");
        }

        private static void Attach(Component radio, TcpClient client, object pairing, bool headset)
        {
            Call(radio, "Initialize", headset);
            Call(radio, "OpenTransport", client, pairing, false);
        }

        private static void Detach(Component radio)
        {
            Call(radio, "ClosePeer");
        }

        private static void CheckDispatch(Component desktop, Component quest, TcpClient laptopPeer, TcpClient questPeer)
        {
            var original = MarsPathDataLoader.LoadMockMission();
            var selected = original.routes[1];
            Type type = typeof(MarsPathDemoBootstrap).GetNestedType("DispatchedPlan", BindingFlags.NonPublic);
            var plan = Activator.CreateInstance(type, true);
            type.GetField("mission").SetValue(plan, original);
            type.GetField("selectedRoute").SetValue(plan, 1);
            type.GetField("demo").SetValue(plan, false);
            var encoded = (byte[])Call(plan, "Encode");
            Require(encoded.Length > PayloadBytes, "The plan must require several encrypted chunks");
            Require((bool)Call(desktop, "QueueRoute", encoded), "Desktop can queue a route before the headset is ready");
            Call(desktop, "SendControlFrames");
            Require(!questPeer.GetStream().DataAvailable, "The route waits for readiness");

            Call(quest, "SetRouteWaiting", true);
            Call(quest, "SendControlFrames");
            Pump(desktop, () => (bool)Property(desktop, "QuestWaiting"));
            Call(desktop, "SendControlFrames");
            object[] delivered = { null, null };
            Pump(quest, () => (bool)Call(quest, "TryTakeRoute", delivered));
            var decoded = type.GetMethod("Decode", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { delivered[1] });
            int index = (int)type.GetField("selectedRoute").GetValue(decoded);
            var snapshot = (MissionData)type.GetField("mission").GetValue(decoded);
            Require(index == 1 && !(bool)type.GetField("demo").GetValue(decoded) &&
                snapshot.routes[index].id == selected.id &&
                snapshot.routes[index].waypoints[1].z == selected.waypoints[1].z &&
                snapshot.destination.longitude == original.destination.longitude,
                "Dispatch preserves the exact selected non-Balanced route, endpoints and mode");

            SendReply("Preparing");
            Require(Property(desktop, "DispatchStatus").ToString() == "Preparing", "Desktop learns that Quest is loading");
            Call(quest, "ResetControlConnection");
            Call(desktop, "ResetControlConnection");
            Call(quest, "SendControlFrames");
            Pump(desktop, () => (bool)Property(desktop, "QuestReadyKnown"));
            Call(desktop, "SendControlFrames");
            Pump(quest, () => (Guid)Get(quest, "replyId") != Guid.Empty);
            Require(!(bool)Call(quest, "TryTakeRoute", new object[] { null, null }), "Reconnect must not deliver the same route twice");
            SendReply("Started");
            Require(!(bool)Property(desktop, "DispatchPending") && Property(desktop, "DispatchStatus").ToString() == "Started",
                "Desktop reports success after the headset acknowledges entering the route");

            Call(desktop, "SetMirrorReady", delivered[0], true);
            Call(desktop, "SendControlFrames");
            Pump(quest, () => (bool)Property(quest, "RemoteMirrorReady"));
            Call(desktop, "SetMirrorReady", delivered[0], false);
            Call(desktop, "SendControlFrames");
            Pump(quest, () => !(bool)Property(quest, "RemoteMirrorReady"));

            var live = System.Text.Encoding.UTF8.GetBytes("{\"session\":\"" + ((Guid)delivered[0]).ToString("N") +
                "\",\"sequence\":17,\"progress\":0.42,\"inMission\":true,\"moving\":false,\"oxygen\":94,\"power\":92}");
            Call(quest, "SendLiveState", live);
            byte[] received = null;
            Pump(desktop, () => (received = (byte[])Call(desktop, "TakeLiveState")) != null);
            Type liveType = desktop.GetType().Assembly.GetType("MarsPath.MarsMissionLiveState", true);
            var decodedState = liveType.GetMethod("Decode", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { received });
            Require((float)liveType.GetField("progress").GetValue(decodedState) == .42f &&
                !(bool)liveType.GetField("moving").GetValue(decodedState), "Live progress and stop state survive encryption");

            void SendReply(string name)
            {
                Type status = desktop.GetType().GetNestedType("RouteDelivery", BindingFlags.NonPublic);
                Call(quest, "ReportRoute", delivered[0], Enum.Parse(status, name));
                Call(quest, "SendControlFrames");
                Pump(desktop, () => Property(desktop, "DispatchStatus").ToString() == name);
            }
        }

        private static void Pump(Component receiver, Func<bool> complete)
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            do
            {
                Call(receiver, "Receive");
                if (complete()) return;
                Thread.Sleep(1);
            } while (DateTime.UtcNow < deadline);
            throw new InvalidOperationException("Mission Control frame timeout");
        }

        private static void CheckVoice(Component sender, Component receiver,
            float expected, bool blocked)
        {
            var payload = new byte[PayloadBytes];
            payload[0] = 1;
            for (int i = 0; i < 960; i++) { payload[1 + i * 2] = 0xcc; payload[2 + i * 2] = 0x0c; }
            Call(receiver, "ClearPlayback");
            object receivingTransport = Get(receiver, "transport");
            int count = (int)Property(receivingTransport, "ReceivedVoiceFrames");
            object sendingTransport = Get(sender, "transport");
            for (int i = 0; i < 10; i++) Call(sendingTransport, "Send", payload, true, false);
            // Deliberately do not tick the Unity receiver while audio arrives.
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while ((int)Property(receivingTransport, "ReceivedVoiceFrames") < count + 10 && DateTime.UtcNow < deadline)
                Thread.Sleep(1);
            Require((int)Property(receivingTransport, "ReceivedVoiceFrames") == count + 10,
                "Background voice reception must continue without rendering");
            var output = new float[3840];
            Call(receiver, "OnAudioFilterRead", output, 2);
            Require(Array.TrueForAll(output[192..], sample => Math.Abs(sample - expected) < .002f),
                blocked ? "Incoming voice must not play into an active microphone" : "Decoded speech has the expected receive gain");
        }

        private static void CheckJitterBuffer()
        {
            Type type = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsVoicePlayback", true);
            object jitter = Activator.CreateInstance(type, true);
            var pcm = new byte[1920];
            for (int i = 0; i < 960; i++) { pcm[i * 2] = 0xcc; pcm[i * 2 + 1] = 0x0c; }
            // Irregular render delivery batches still provide exactly 50 frames each second.
            int[] deliveries = { 4, 0, 1, 0, 2, 0, 1, 0, 1, 1 };
            int totalWritten = 0, totalRead = 0;
            for (int step = 0; step < 200; step++)
            {
                int frames = deliveries[step % deliveries.Length];
                for (int i = 0; i < frames; i++) { Call(jitter, "Write", pcm, 0, 960, true); totalWritten += 960; }
                if (step < 8) continue;
                var output = new float[960];
                Call(jitter, "Read", output);
                totalRead += output.Length;
                Require(Array.TrueForAll(output[96..], sample => Math.Abs(sample - .36f) < .002f),
                    "Jitter buffer must keep continuous speech across irregular packet batches");
            }
            Require((int)Property(jitter, "Underflows") == 0 && (int)Property(jitter, "Overflows") == 0,
                "Steady speech must not underrun or overflow");
            Require((int)Property(jitter, "BufferedSamples") == totalWritten - totalRead, "Buffered sample conservation");
        }

        private static object Call(object target, string method, params object[] arguments)
            => target.GetType().GetMethod(method, Private).Invoke(target, arguments);
        private static object Property(object target, string name) => target.GetType().GetProperty(name, Private).GetValue(target);
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CheckRouteFacing()
        {
            Type type = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsRouteFacing", true);
            object gate = Activator.CreateInstance(type, true);
            Call(gate, "Update", true, 180f);
            Require((bool)Property(gate, "Blocked") && (bool)Property(gate, "RouteBehind"), "Looking backwards locks walking");
            Call(gate, "Update", true, 65f);
            Require((bool)Property(gate, "Blocked"), "Partial turn retains the lock until facing the route");
            Call(gate, "Update", true, 45f);
            Require(!(bool)Property(gate, "Blocked"), "Facing the route releases the lock");
            Call(gate, "Update", true, 70f);
            Require(!(bool)Property(gate, "Blocked"), "Heading hysteresis tolerates small boundary movement");
            Call(gate, "Update", true, -100f);
            Require((bool)Property(gate, "Blocked") && ((string)Property(gate, "Instruction")).Contains("LEFT"),
                "Off-route warning gives the correct turn direction");
            Call(gate, "Update", false, 0f);
            Require((bool)Property(gate, "Blocked"), "No valid heading cannot authorize a walk");
            MethodInfo angle = type.GetMethod("TrySignedError", BindingFlags.Static | BindingFlags.NonPublic);
            object[] tilted = { Quaternion.Euler(62, 180, 12) * Vector3.forward, Vector3.forward, 0f };
            Require((bool)angle.Invoke(null, tilted) && Mathf.Abs(Mathf.Abs((float)tilted[2]) - 180) < .01f,
                "Pitch and roll do not disguise a backwards horizontal heading");
            object[] vertical = { Vector3.up, Vector3.forward, 0f };
            Require(!(bool)angle.Invoke(null, vertical), "A vertical gaze has no valid horizontal heading");
            Debug.Log("MARSPATH_FACING_GATE_PASSED: opposite heading, recovery, hysteresis, turn direction and invalid tracking.");
        }

        private static void CheckCompass()
        {
            Type type = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsCompass", true);
            const BindingFlags methods = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo getHeading = type.GetMethod("TryGetHeading", methods);
            MethodInfo format = type.GetMethod("Format", methods);
            foreach (var sample in new[] { (0f, "N  000°"), (90f, "E  090°"), (180f, "S  180°"),
                (270f, "W  270°"), (315f, "NW  315°"), (359.8f, "N  000°") })
            {
                object[] args = { Quaternion.Euler(48, sample.Item1, -19) * Vector3.forward, 0f };
                Require((bool)getHeading.Invoke(null, args) && (string)format.Invoke(null, new[] { args[1] }) == sample.Item2,
                    "Compass uses the horizontal gaze with pitch and roll and wraps north correctly");
            }
            object[] vertical = { Vector3.up, 0f };
            Require(!(bool)getHeading.Invoke(null, vertical), "Vertical gaze cannot produce an unstable compass bearing");
            Debug.Log("MARSPATH_COMPASS_PASSED: N, E, S, W, NW, north wrap, and vertical gaze.");
        }

        private static void CheckSpeechCleanup()
        {
            Type type = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsVoiceSpeechFilter", true);
            double passband = GainAt(500), rumble = GainAt(20), hiss = GainAt(14000);
            Require(passband > .95 && passband < 1.02, "Speech cleanup preserves the voice band");
            Require(rumble < .12 && hiss < .12, "Speech cleanup suppresses low rumble and high-frequency hiss");

            Type playbackType = typeof(MarsPathDemoBootstrap).Assembly.GetType("MarsPath.MarsVoicePlayback", true);
            object buffer = Activator.CreateInstance(playbackType, true);
            var loud = new byte[1920];
            for (int i = 0; i < 960; i++) { loud[i * 2] = 0xf0; loud[i * 2 + 1] = 0x7f; }
            for (int i = 0; i < 10; i++) Call(buffer, "Write", loud, 0, 960, true);
            var output = new float[1920];
            Call(buffer, "Read", output);
            Require(Array.TrueForAll(output[96..], sample => sample > .7f && sample < .8f),
                "Strong input retains headroom instead of continuous high-gain saturation");
            Debug.Log($"MARSPATH_SPEECH_FILTER: 500Hz={passband:F3}, 20Hz={rumble:F3}, 14kHz={hiss:F3}; strong speech headroom passed.");

            double GainAt(int frequency)
            {
                object filter = Activator.CreateInstance(type, true);
                var samples = new float[9600];
                for (int i = 0; i < samples.Length; i++) samples[i] = .1f * (float)Math.Sin(2 * Math.PI * frequency * i / 48000);
                Call(filter, "Process", samples);
                double sum = 0;
                for (int i = 4800; i < samples.Length; i++) sum += samples[i] * samples[i];
                return Math.Sqrt(sum / 4800) / (.1 / Math.Sqrt(2));
            }
        }
    }
}
