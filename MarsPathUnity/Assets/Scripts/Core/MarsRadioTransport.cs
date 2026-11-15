using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MarsPath
{
    /// <summary>One ordered reader and writer keep encrypted traffic independent of rendering.</summary>
    internal sealed class MarsRadioTransport : IDisposable
    {
        private const int MaximumQueuedVoiceFrames = 6;
        private readonly TcpClient client;
        private readonly MarsRadioPairing pairing;
        private readonly int payloadBytes;
        private readonly byte stateKind;
        private readonly Action<byte[]> receiveVoice;
        private readonly ConcurrentQueue<byte[]> incoming = new ConcurrentQueue<byte[]>();
        private readonly Queue<byte[]> controls = new Queue<byte[]>(), voice = new Queue<byte[]>();
        private readonly object sendLock = new object();
        private readonly AutoResetEvent ready = new AutoResetEvent(false);
        private readonly Task reader, writer;
        private byte[] outgoingState, incomingState;
        private volatile bool closed;
        private int disposed;
        private int receivedVoiceFrames;

        internal bool Connected => !closed;
        internal bool CanSendControl { get { lock (sendLock) return !closed && controls.Count < 32; } }
        internal int DroppedVoiceFrames { get; private set; }
        internal int ReceivedVoiceFrames => Volatile.Read(ref receivedVoiceFrames);
        internal string Failure { get; private set; }

        internal MarsRadioTransport(TcpClient client, MarsRadioPairing pairing, int payloadBytes, byte stateKind,
            Action<byte[]> receiveVoice)
        {
            this.client = client;
            this.pairing = pairing;
            this.payloadBytes = payloadBytes;
            this.stateKind = stateKind;
            this.receiveVoice = receiveVoice;
            client.NoDelay = true;
            client.SendBufferSize = 65536;
            client.ReceiveBufferSize = 65536;
            client.GetStream().WriteTimeout = 2000;
            reader = Task.Run(() => Run(Read));
            writer = Task.Run(() => Run(Write));
        }

        internal bool Send(byte[] payload, bool isVoice, bool latestState = false)
        {
            lock (sendLock)
            {
                if (closed) return false;
                byte[] copy = (byte[])payload.Clone();
                if (latestState) outgoingState = copy;
                else if (isVoice)
                {
                    if (voice.Count >= MaximumQueuedVoiceFrames) { voice.Dequeue(); DroppedVoiceFrames++; }
                    voice.Enqueue(copy);
                }
                else
                {
                    if (controls.Count >= 64) return false;
                    controls.Enqueue(copy);
                }
            }
            ready.Set();
            return true;
        }

        internal bool TryReceive(out byte[] payload)
        {
            if (incoming.TryDequeue(out payload)) return true;
            payload = Interlocked.Exchange(ref incomingState, null);
            return payload != null;
        }

        private void Read()
        {
            var frame = new byte[MarsRadioPairing.FrameLength(payloadBytes)];
            NetworkStream stream = client.GetStream();
            while (!closed)
            {
                int offset = 0;
                while (offset < frame.Length)
                {
                    int count = stream.Read(frame, offset, frame.Length - offset);
                    if (count == 0) throw new EndOfStreamException("Mission Control disconnected.");
                    offset += count;
                }
                var payload = new byte[payloadBytes];
                if (!pairing.TryDecrypt(frame, payload)) throw new InvalidDataException("Invalid Mission Control frame.");
                if (payload[0] == 1)
                {
                    receiveVoice(payload);
                    Interlocked.Increment(ref receivedVoiceFrames);
                }
                else if (payload[0] == stateKind) Interlocked.Exchange(ref incomingState, payload);
                else
                {
                    if (incoming.Count >= 256) throw new InvalidDataException("Mission Control receive queue is full.");
                    incoming.Enqueue(payload);
                }
            }
        }

        private void Write()
        {
            var frame = new byte[MarsRadioPairing.FrameLength(payloadBytes)];
            NetworkStream stream = client.GetStream();
            int burst = 0;
            while (!closed)
            {
                byte[] payload = null;
                lock (sendLock)
                {
                    if (voice.Count > 0 && (burst < 4 || controls.Count == 0 && outgoingState == null))
                    {
                        payload = voice.Dequeue();
                        burst++;
                    }
                    else if (controls.Count > 0) { payload = controls.Dequeue(); burst = 0; }
                    else if (outgoingState != null) { payload = outgoingState; outgoingState = null; burst = 0; }
                }
                if (payload == null) { ready.WaitOne(20); continue; }
                pairing.Encrypt(payload, frame);
                stream.Write(frame, 0, frame.Length);
            }
        }

        private void Run(Action operation)
        {
            try { operation(); }
            catch (Exception error) when (error is IOException || error is SocketException ||
                error is ObjectDisposedException || error is System.Security.Cryptography.CryptographicException)
            { if (!closed) Failure = error.GetType().Name; }
            finally
            {
                closed = true;
                client.Close();
                ready.Set();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            closed = true;
            client.Close();
            ready.Set();
            _ = Task.WhenAll(reader, writer).ContinueWith(_ =>
            {
                pairing.Dispose();
                ready.Dispose();
            }, TaskScheduler.Default);
        }
    }
}
