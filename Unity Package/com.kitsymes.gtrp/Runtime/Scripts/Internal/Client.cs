using KitSymes.GTRP.Packets;
using System;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace KitSymes.GTRP.Internal
{
    public class Client
    {
        protected NetworkManager _networkManager;
        protected TcpClient _tcpClient;
        protected bool _running = false;

        private SemaphoreSlim _tcpWriteAsyncLock;

        protected RSACryptoServiceProvider _rsaProvider;
        protected RSAParameters _publicKey;
        protected RSAParameters _privateKey;
        protected RSAParameters _otherPublicKey;

        public Client(NetworkManager networkManager)
        {
            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);

            _rsaProvider = new RSACryptoServiceProvider(512);
            _publicKey = _rsaProvider.ExportParameters(false);
            _privateKey = _rsaProvider.ExportParameters(true);
            _networkManager = networkManager;
        }

        protected async Task WriteTCP(byte[] data)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                await _tcpClient.GetStream().WriteAsync(data);
                // Flush Stream
                await _tcpClient.GetStream().FlushAsync();
            }
            finally
            {
                _tcpWriteAsyncLock.Release();
            }
        }

        protected async Task WriteTCP(Packet packet, bool useEncryption = false)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Encrypt the data if needed
                if (useEncryption)
                {
                    // Import the other side's public key
                    _rsaProvider.ImportParameters(_otherPublicKey);
                    // Encrypt the packet
                    buffer = _rsaProvider.Encrypt(buffer, true);
                    // Create a new packet so the other side knows it's encrypted
                    PacketEncrypted packetEncrypted = new PacketEncrypted() { encryptedData = buffer };
                    // Serialise the encrypted packet
                    buffer = PacketFormatter.Serialise(packetEncrypted);
                }
                // Send Packet Size
                await _tcpClient.GetStream().WriteAsync(BitConverter.GetBytes((uint)buffer.Length));

                // Send Packet Data
                await _tcpClient.GetStream().WriteAsync(buffer);

                // Flush Stream
                await _tcpClient.GetStream().FlushAsync();
            }
            finally
            {
                _tcpWriteAsyncLock.Release();
            }
        }

        protected async Task<Packet> ReadTCP()
        {
            // Read until a packet size is encountered
            byte[] sizeBuffer = new byte[sizeof(uint)];
            int bytesRead = await _tcpClient.GetStream().ReadAsync(sizeBuffer);
#if UNITY_EDITOR
            _networkManager.bytesRead += bytesRead;
#endif
            if (bytesRead <= 0)
            {
                return null;
            }
            else if (bytesRead < sizeBuffer.Length)
            {
                throw new ClientException("Invalid sizeBuffer, read " + bytesRead + " bytes when it should be " + sizeBuffer.Length);
            }
            uint bufferSize = BitConverter.ToUInt32(sizeBuffer);

            // Try and read the whole packet
            byte[] packetBuffer = new byte[bufferSize];
            bytesRead = await _tcpClient.GetStream().ReadAsync(packetBuffer);
#if UNITY_EDITOR
            _networkManager.bytesRead += bytesRead;
#endif
            if (bytesRead <= 0)
            {
                return null;
            }
            else if (bytesRead < packetBuffer.Length)
            {
                throw new ClientException("Invalid packetBuffer, read " + bytesRead + " bytes when it should be " + packetBuffer.Length);
            }

            // Deserialise Packet
            Packet packet = PacketFormatter.Deserialise(packetBuffer);
            // If the Packet is Encrypted
            if (packet is PacketEncrypted encrypted)
            {
                // Import the other side's public key
                _rsaProvider.ImportParameters(_privateKey);
                // Encrypt the packet
                byte[] buffer = _rsaProvider.Decrypt(encrypted.encryptedData, true);

                return PacketFormatter.Deserialise(buffer);
            }
            return packet;
        }
    }
}
