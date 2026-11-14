using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace MarsPath
{
    internal sealed partial class MarsMissionRadio
    {
        private enum MessageKind : byte { Voice = 1, WaitState, RouteHeader, RouteData, RouteReply, LiveState, MirrorReady }

        internal enum RouteDelivery : byte
        {
            Idle, Waiting, Sending, Preparing, Started, Busy, Failed, TimedOut
        }

        internal const int MaximumRouteBytes = 10 * 1024 * 1024;
        private const int IdBytes = 16;
        private const int ChunkBytes = PayloadBytes - DataOffset;
        private const int HeaderValueOffset = DataOffset + IdBytes;

        private bool routeWaiting, remoteWaiting, remoteStateKnown;
        private bool? advertisedWaiting;
        private Guid sentRouteId, incomingRouteId, lastReceivedId, replyId;
        private byte[] queuedRoute, incomingRoute, completedRoute;
        private Guid completedRouteId;
        private int sentBytes, remainingBytes;
        private bool headerSent, attemptedDispatch;
        private float dispatchDeadline;
        private RouteDelivery lastReceivedStatus, replyStatus;
        private Guid mirrorSession, readySession;
        private bool mirrorReady, receivedMirrorReady;
        private float nextReadyNotice;
        private float readyReceivedAt = -10;
        private byte[] receivedLiveState;

        internal bool QuestWaiting => Connected && remoteWaiting;
        internal bool QuestReadyKnown => Connected && remoteStateKnown;
        internal bool DispatchPending => queuedRoute != null;
        internal RouteDelivery DispatchStatus { get; private set; }
        internal Guid DispatchId => sentRouteId;
        internal bool RemoteMirrorReady => Connected && receivedMirrorReady && readySession == lastReceivedId &&
            Time.unscaledTime - readyReceivedAt < 1.5f;

        internal void SetRouteWaiting(bool waiting) => routeWaiting = waiting;

        internal bool QueueRoute(byte[] route)
        {
            if (headset || DispatchPending || route == null || route.Length == 0 || route.Length > MaximumRouteBytes)
                return false;
            queuedRoute = route;
            sentRouteId = Guid.NewGuid();
            receivedLiveState = null;
            attemptedDispatch = headerSent = false;
            sentBytes = 0;
            dispatchDeadline = Time.unscaledTime + 30f;
            DispatchStatus = RouteDelivery.Waiting;
            return true;
        }

        internal bool TryTakeRoute(out Guid id, out byte[] route)
        {
            id = completedRouteId;
            route = completedRoute;
            completedRoute = null;
            return route != null;
        }

        internal void ReportRoute(Guid id, RouteDelivery status)
        {
            if (id == lastReceivedId) lastReceivedStatus = status;
            replyId = id;
            replyStatus = status;
        }

        internal void SetMirrorReady(Guid session, bool value)
        {
            if (mirrorSession != session || mirrorReady != value) nextReadyNotice = 0;
            mirrorSession = session;
            mirrorReady = value;
        }

        internal void SendLiveState(byte[] encoded)
        {
            if (!Connected || encoded.Length > PayloadBytes - 3) return;
            BeginFrame(MessageKind.LiveState);
            BinaryPrimitives.WriteUInt16LittleEndian(outgoingPayload.AsSpan(1), (ushort)encoded.Length);
            Buffer.BlockCopy(encoded, 0, outgoingPayload, 3, encoded.Length);
            transport.Send(outgoingPayload, false, true);
        }

        internal byte[] TakeLiveState()
        {
            byte[] state = receivedLiveState;
            receivedLiveState = null;
            return state;
        }

        private void CheckDispatchTimeout(float now)
        {
            if (DispatchPending && now >= dispatchDeadline)
                FinishDispatch(RouteDelivery.TimedOut);
        }

        private void FinishDispatch(RouteDelivery status)
        {
            queuedRoute = null;
            DispatchStatus = status;
        }

        private void SendControlFrames()
        {
            if (transport?.CanSendControl != true) return;
            if (headset)
            {
                if (advertisedWaiting != routeWaiting)
                {
                    BeginFrame(MessageKind.WaitState);
                    outgoingPayload[DataOffset] = routeWaiting ? (byte)1 : (byte)0;
                    WriteFrame();
                    advertisedWaiting = routeWaiting;
                }
                if (replyId != Guid.Empty && transport.CanSendControl)
                {
                    BeginFrame(MessageKind.RouteReply);
                    EncodeId(replyId);
                    outgoingPayload[HeaderValueOffset] = (byte)replyStatus;
                    WriteFrame();
                    replyId = Guid.Empty;
                }
                return;
            }

            if (mirrorSession != Guid.Empty && Time.unscaledTime >= nextReadyNotice)
            {
                BeginFrame(MessageKind.MirrorReady);
                EncodeId(mirrorSession);
                outgoingPayload[HeaderValueOffset] = mirrorReady ? (byte)1 : (byte)0;
                WriteFrame();
                nextReadyNotice = Time.unscaledTime + .5f;
            }
            if (!DispatchPending || !remoteWaiting && !attemptedDispatch) return;
            if (!headerSent)
            {
                BeginFrame(MessageKind.RouteHeader);
                EncodeId(sentRouteId);
                BinaryPrimitives.WriteInt32LittleEndian(outgoingPayload.AsSpan(HeaderValueOffset), queuedRoute.Length);
                WriteFrame();
                attemptedDispatch = headerSent = true;
                if (DispatchStatus != RouteDelivery.Preparing) DispatchStatus = RouteDelivery.Sending;
            }
            // A fixed budget per frame leaves room for push-to-talk.
            for (int i = 0; i < 6 && sentBytes < queuedRoute.Length && transport.CanSendControl; i++)
            {
                int size = Math.Min(ChunkBytes, queuedRoute.Length - sentBytes);
                BeginFrame(MessageKind.RouteData);
                Buffer.BlockCopy(queuedRoute, sentBytes, outgoingPayload, DataOffset, size);
                WriteFrame();
                sentBytes += size;
            }
        }

        private bool ReceiveControlFrame()
        {
            switch ((MessageKind)incomingPayload[0])
            {
                case MessageKind.WaitState when !headset:
                    if (incomingPayload[DataOffset] > 1) return false;
                    remoteWaiting = incomingPayload[DataOffset] == 1;
                    remoteStateKnown = true;
                    return true;
                case MessageKind.RouteHeader when headset:
                    return ReceiveRouteHeader();
                case MessageKind.RouteData when headset:
                    return ReceiveRouteChunk();
                case MessageKind.RouteReply when !headset:
                    return ReceiveRouteReply();
                case MessageKind.MirrorReady when headset:
                    readySession = DecodeId();
                    receivedMirrorReady = incomingPayload[HeaderValueOffset] == 1;
                    readyReceivedAt = Time.unscaledTime;
                    return true;
                case MessageKind.LiveState when !headset:
                    int size = BinaryPrimitives.ReadUInt16LittleEndian(incomingPayload.AsSpan(1));
                    if (size == 0 || size > PayloadBytes - 3) return false;
                    receivedLiveState = new byte[size];
                    Buffer.BlockCopy(incomingPayload, 3, receivedLiveState, 0, size);
                    return true;
                default:
                    return false;
            }
        }

        private bool ReceiveRouteHeader()
        {
            incomingRouteId = DecodeId();
            remainingBytes = BinaryPrimitives.ReadInt32LittleEndian(incomingPayload.AsSpan(HeaderValueOffset));
            if (incomingRouteId == Guid.Empty || remainingBytes <= 0 || remainingBytes > MaximumRouteBytes) return false;

            incomingRoute = null;
            if (incomingRouteId == lastReceivedId) ReportRoute(incomingRouteId, lastReceivedStatus);
            else if (!routeWaiting || completedRoute != null) ReportRoute(incomingRouteId, RouteDelivery.Busy);
            else incomingRoute = new byte[remainingBytes];
            return true;
        }

        private bool ReceiveRouteChunk()
        {
            if (remainingBytes <= 0) return false;
            int size = Math.Min(ChunkBytes, remainingBytes);
            if (incomingRoute != null)
                Buffer.BlockCopy(incomingPayload, DataOffset, incomingRoute, incomingRoute.Length - remainingBytes, size);
            remainingBytes -= size;
            if (remainingBytes != 0 || incomingRoute == null) return true;

            byte[] assembled = incomingRoute;
            incomingRoute = null;
            if (!routeWaiting || completedRoute != null)
            {
                ReportRoute(incomingRouteId, RouteDelivery.Busy);
                return true;
            }
            completedRoute = assembled;
            completedRouteId = lastReceivedId = incomingRouteId;
            lastReceivedStatus = RouteDelivery.Preparing;
            routeWaiting = false;
            receivedMirrorReady = false;
            return true;
        }

        private bool ReceiveRouteReply()
        {
            var status = (RouteDelivery)incomingPayload[HeaderValueOffset];
            if (status != RouteDelivery.Preparing && status != RouteDelivery.Started &&
                status != RouteDelivery.Busy && status != RouteDelivery.Failed) return false;
            if (DispatchPending && DecodeId() == sentRouteId)
            {
                if (status == RouteDelivery.Preparing)
                {
                    if (DispatchStatus != status) dispatchDeadline = Time.unscaledTime + 60f;
                    DispatchStatus = status;
                }
                else FinishDispatch(status);
            }
            return true;
        }

        private void ResetControlConnection()
        {
            advertisedWaiting = null;
            remoteWaiting = remoteStateKnown = headerSent = false;
            sentBytes = remainingBytes = 0;
            incomingRoute = null;
            replyId = Guid.Empty;
            receivedMirrorReady = false;
            nextReadyNotice = 0;
        }

        private void BeginFrame(MessageKind kind)
        {
            Array.Clear(outgoingPayload, 0, outgoingPayload.Length);
            outgoingPayload[0] = (byte)kind;
        }

        private void WriteFrame()
        {
            if (!transport.Send(outgoingPayload, (MessageKind)outgoingPayload[0] == MessageKind.Voice))
                throw new IOException("Mission Control send queue is unavailable.");
        }

        private void EncodeId(Guid id) => id.TryWriteBytes(outgoingPayload.AsSpan(DataOffset, IdBytes));
        private Guid DecodeId() => new Guid(incomingPayload.AsSpan(DataOffset, IdBytes));
    }
}
