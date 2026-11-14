using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace MarsPath
{
    // Both ends prove possession of the key provisioned during USB setup.
    internal sealed class MarsRadioPairing : IDisposable
    {
        internal const int KeyLength = 32;
        private const int NonceLength = 32;
        private const byte ProtocolVersion = 4;
        private const int IvLength = 16;
        private const int TagLength = 32;
        private readonly VoiceChannel outgoing, incoming;

        private MarsRadioPairing(VoiceChannel outgoing, VoiceChannel incoming)
        {
            this.outgoing = outgoing;
            this.incoming = incoming;
        }

        internal static int FrameLength(int pcmLength) => IvLength + pcmLength + TagLength;

        internal static MarsRadioPairing Authenticate(TcpClient peer, byte[] key, bool headset, int payloadLength)
        {
            NetworkStream stream = peer.GetStream();
            stream.ReadTimeout = 4000;
            stream.WriteTimeout = 4000;

            // Derive different proofs and keys for each role, direction and purpose.
            byte[] nonces = new byte[2 * NonceLength + 2];
            nonces[2 * NonceLength] = ProtocolVersion;
            byte[] ownNonce = new byte[NonceLength];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(ownNonce);
            using var proof = new HMACSHA256(key);
            byte[] received = new byte[TagLength];

            if (headset)
            {
                Buffer.BlockCopy(ownNonce, 0, nonces, 0, NonceLength);
                stream.Write(ownNonce, 0, NonceLength);
                ReadExactly(stream, nonces, NonceLength, NonceLength);
                ReadExactly(stream, received, 0, received.Length);
                if (!Equal(Derive(1), received, 0)) throw new CryptographicException("Unpaired radio.");
                SendProof(2);
            }
            else
            {
                ReadExactly(stream, nonces, 0, NonceLength);
                Buffer.BlockCopy(ownNonce, 0, nonces, NonceLength, NonceLength);
                stream.Write(ownNonce, 0, NonceLength);
                SendProof(1);
                ReadExactly(stream, received, 0, received.Length);
                if (!Equal(Derive(2), received, 0)) throw new CryptographicException("Unpaired radio.");
            }

            stream.ReadTimeout = Timeout.Infinite;
            stream.WriteTimeout = 2000;
            var laptopToQuest = new VoiceChannel(Derive(3), Derive(4), payloadLength);
            var questToLaptop = new VoiceChannel(Derive(5), Derive(6), payloadLength);
            return headset ? new MarsRadioPairing(questToLaptop, laptopToQuest)
                : new MarsRadioPairing(laptopToQuest, questToLaptop);

            byte[] Derive(byte purpose)
            {
                nonces[2 * NonceLength + 1] = purpose;
                return proof.ComputeHash(nonces);
            }

            void SendProof(byte purpose)
            {
                byte[] bytes = Derive(purpose);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        internal void Encrypt(byte[] pcm, byte[] frame) => outgoing.Encrypt(pcm, frame);
        internal bool TryDecrypt(byte[] frame, byte[] pcm) => incoming.TryDecrypt(frame, pcm);

        internal static bool Equal(byte[] expected, byte[] received, int offset)
        {
            int difference = 0;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ received[offset + i];
            return difference == 0;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int start, int count)
        {
            int end = start + count;
            while (start < end)
            {
                int read = stream.Read(buffer, start, end - start);
                if (read == 0) throw new EndOfStreamException("The radio closed during pairing.");
                start += read;
            }
        }

        public void Dispose()
        {
            outgoing.Dispose();
            incoming.Dispose();
        }

        private sealed class VoiceChannel : IDisposable
        {
            private const int CounterLength = 8;
            private readonly Aes cipher;
            private readonly HMACSHA256 mac;
            private readonly RandomNumberGenerator random = RandomNumberGenerator.Create();
            private readonly byte[] encryptionKey, signedFrame;
            private readonly byte[] iv = new byte[IvLength];
            private readonly int payloadLength;
            private ulong counter;

            internal VoiceChannel(byte[] encryptionKey, byte[] authenticationKey, int payloadLength)
            {
                this.encryptionKey = encryptionKey;
                this.payloadLength = payloadLength;
                cipher = Aes.Create();
                cipher.Mode = CipherMode.CBC;
                cipher.Padding = PaddingMode.None;
                mac = new HMACSHA256(authenticationKey);
                signedFrame = new byte[CounterLength + IvLength + payloadLength];
            }

            internal void Encrypt(byte[] pcm, byte[] frame)
            {
                random.GetBytes(iv);
                Buffer.BlockCopy(iv, 0, frame, 0, IvLength);
                using (var encrypt = cipher.CreateEncryptor(encryptionKey, iv))
                    encrypt.TransformBlock(pcm, 0, payloadLength, frame, IvLength);
                byte[] tag = Sign(frame);
                Buffer.BlockCopy(tag, 0, frame, IvLength + payloadLength, TagLength);
                counter++;
            }

            internal bool TryDecrypt(byte[] frame, byte[] pcm)
            {
                if (!Equal(Sign(frame), frame, IvLength + payloadLength)) return false;
                Buffer.BlockCopy(frame, 0, iv, 0, IvLength);
                using var decrypt = cipher.CreateDecryptor(encryptionKey, iv);
                byte[] decrypted = decrypt.TransformFinalBlock(frame, IvLength, payloadLength);
                Buffer.BlockCopy(decrypted, 0, pcm, 0, payloadLength);
                counter++;
                return true;
            }

            private byte[] Sign(byte[] frame)
            {
                for (int i = 0; i < CounterLength; i++) signedFrame[i] = (byte)(counter >> (i * 8));
                Buffer.BlockCopy(frame, 0, signedFrame, CounterLength, IvLength + payloadLength);
                return mac.ComputeHash(signedFrame);
            }

            public void Dispose()
            {
                cipher.Dispose();
                mac.Dispose();
                random.Dispose();
            }
        }
    }
}
