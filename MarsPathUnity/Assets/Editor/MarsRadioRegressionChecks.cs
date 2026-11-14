using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MarsPath.Editor
{
    public static class MarsRadioRegressionChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

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
            Debug.Log("MARSPATH_RADIO: available laptop microphone selection=" +
                select.Invoke(null, new object[] { Microphone.devices, false }));
            MethodInfo amplify = radioType.GetMethod("AmplifyVoice", BindingFlags.Static | BindingFlags.NonPublic);
            Require((float)amplify.Invoke(null, new object[] { (short)3276 }) > .22f, "Quiet speech gain");
            Require(Math.Abs((float)amplify.Invoke(null, new object[] { short.MinValue })) < .91f, "Peak limiter");

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using var desktop = new TcpClient();
            desktop.Connect(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            using TcpClient headset = listener.AcceptTcpClient();
            listener.Stop();
            MethodInfo authenticate = pairingType.GetMethod("Authenticate", BindingFlags.Static | BindingFlags.NonPublic);
            var key = new byte[32];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(i + 1);
            Task<object> headsetPairing = Task.Run(() => authenticate.Invoke(null, new object[] { headset, key, true, 1920 }));
            using var desktopPairing = (IDisposable)authenticate.Invoke(null, new object[] { desktop, key, false, 1920 });
            Require(headsetPairing.Wait(6000), "Pairing timeout");
            using var questPairing = (IDisposable)headsetPairing.Result;

            CheckDirection(radioType, pairingType, desktop, headset, desktopPairing, questPairing, true);
            CheckDirection(radioType, pairingType, headset, desktop, questPairing, desktopPairing, false);
            Debug.Log("MARSPATH_RADIO_REGRESSION_PASSED: encrypted laptop -> Quest and Quest -> laptop playback, including receiving while holding talk; physical mic selection; speech gain and limiter.");
        }

        private static void CheckDirection(Type radioType, Type pairingType, TcpClient sender, TcpClient receiver,
            object senderPairing, object receiverPairing, bool onHeadset)
        {
            var root = new GameObject("Radio regression receiver");
            Component radio = root.AddComponent(radioType);
            AudioClip fakeCapture = null;
            bool previousAudioPause = AudioListener.pause;
            try
            {
                // Keep the real-time output callback from draining samples before this assertion.
                AudioListener.pause = true;
                radioType.GetMethod("Initialize", Private).Invoke(radio, new object[] { onHeadset });
                radioType.GetField("peer", Private).SetValue(radio, receiver);
                radioType.GetField("pairing", Private).SetValue(radio, receiverPairing);
                // A held local talk button must never discard incoming frames.
                fakeCapture = AudioClip.Create("Held talk regression", 960, 1, 48000, false);
                radioType.GetField("microphone", Private).SetValue(radio, fakeCapture);
                var pcm = new byte[1920];
                for (int i = 0; i < 960; i++) { pcm[i * 2] = 0xcc; pcm[i * 2 + 1] = 0x0c; }
                var frame = new byte[1968];
                MethodInfo encrypt = pairingType.GetMethod("Encrypt", Private);
                for (int i = 0; i < 2; i++)
                {
                    encrypt.Invoke(senderPairing, new object[] { pcm, frame });
                    sender.GetStream().Write(frame, 0, frame.Length);
                }
                Require(receiver.Client.Poll(2000000, SelectMode.SelectRead), "Voice frame timeout");
                MethodInfo receive = radioType.GetMethod("Receive", Private);
                var deadline = DateTime.UtcNow.AddSeconds(2);
                while ((int)radioType.GetField("buffered", Private).GetValue(radio) < 1920 && DateTime.UtcNow < deadline)
                    receive.Invoke(radio, new object[] { receiver.GetStream() });
                var output = new float[1920];
                radioType.GetMethod("ReadVoice", Private).Invoke(radio, new object[] { output });
                Require(Array.TrueForAll(output, sample => sample > .22f && sample < .24f), "Decoded audible speech in both directions");
            }
            finally
            {
                // Socket and key owners are disposed by the outer test.
                radioType.GetField("peer", Private).SetValue(radio, null);
                radioType.GetField("pairing", Private).SetValue(radio, null);
                radioType.GetField("microphone", Private).SetValue(radio, null);
                UnityEngine.Object.DestroyImmediate(root);
                if (fakeCapture != null) UnityEngine.Object.DestroyImmediate(fakeCapture);
                AudioListener.pause = previousAudioPause;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
