using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace MarsPath
{
    internal sealed class MarsRadioDiscovery : IDisposable
    {
        internal const int Port = 42740;
        private const int NonceLength = 16;
        private const int HeaderLength = 4;
        private const int TagLength = 32;
        private const int SignedLength = HeaderLength + NonceLength;
        private const byte Request = 1, Response = 2;
        private readonly UdpClient socket;
        private readonly HMACSHA256 mac;
        private readonly byte[] nonce = new byte[NonceLength];
        private readonly bool headset;
        private float nextProbe;

        internal MarsRadioDiscovery(byte[] key, bool headset)
        {
            this.headset = headset;
            socket = new UdpClient(new IPEndPoint(IPAddress.Any, headset ? Port : 0));
            socket.EnableBroadcast = !headset;
            mac = new HMACSHA256(key);
        }

        internal IPAddress Poll(float now, bool probe)
        {
            if (!headset && probe && now >= nextProbe)
            {
                nextProbe = now + 1.5f;
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonce);
                byte[] request = Create(Request, nonce, 0);
                foreach (IPAddress address in BroadcastAddresses())
                {
                    try { socket.Send(request, request.Length, new IPEndPoint(address, Port)); }
                    catch (SocketException) { } // An interface can disappear during a Wi-Fi change.
                }
            }
            for (int i = 0; i < 8 && socket.Available > 0; i++)
            {
                var origin = new IPEndPoint(IPAddress.Any, 0);
                byte[] packet = socket.Receive(ref origin);
                if (IPAddress.IsLoopback(origin.Address) || !IsValid(packet, headset ? Request : Response))
                    continue;
                if (headset)
                {
                    byte[] response = Create(Response, packet, HeaderLength);
                    socket.Send(response, response.Length, origin);
                }
                else if (origin.Port == Port && MarsRadioPairing.Equal(nonce, packet, HeaderLength))
                    return origin.Address;
            }
            return null;
        }

        private byte[] Create(byte purpose, byte[] source, int offset)
        {
            var packet = new byte[SignedLength + TagLength];
            packet[0] = (byte)'M';
            packet[1] = (byte)'P';
            packet[2] = 1;
            packet[3] = purpose;
            Buffer.BlockCopy(source, offset, packet, HeaderLength, NonceLength);
            Buffer.BlockCopy(mac.ComputeHash(packet, 0, SignedLength), 0, packet, SignedLength, TagLength);
            return packet;
        }

        private bool IsValid(byte[] packet, byte purpose) =>
            packet.Length == SignedLength + TagLength &&
            packet[0] == 'M' && packet[1] == 'P' && packet[2] == 1 && packet[3] == purpose &&
            MarsRadioPairing.Equal(mac.ComputeHash(packet, 0, SignedLength), packet, SignedLength);

        private static HashSet<IPAddress> BroadcastAddresses()
        {
            var result = new HashSet<IPAddress> { IPAddress.Broadcast };
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    foreach (UnicastIPAddressInformation unicast in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask == null) continue;
                        byte[] bytes = unicast.Address.GetAddressBytes(), mask = unicast.IPv4Mask.GetAddressBytes();
                        bool hasHostBits = false;
                        for (int j = 0; j < bytes.Length; j++)
                        {
                            bytes[j] |= (byte)~mask[j];
                            hasHostBits |= mask[j] != 255;
                        }
                        if (hasHostBits) result.Add(new IPAddress(bytes));
                    }
                }
            }
            catch (NetworkInformationException) { }
            return result;
        }

        public void Dispose()
        {
            socket.Close();
            mac.Dispose();
        }
    }
}
