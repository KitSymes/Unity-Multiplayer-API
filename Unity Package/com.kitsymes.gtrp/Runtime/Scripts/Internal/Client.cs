using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace KitSymes.GTRP.Internal
{
    public class Client
    {
        protected BinaryFormatter _formatter;
        protected TcpClient _tcpClient;

        private SemaphoreSlim _tcpWriteAsyncLock;

        public Client()
        {
            _formatter = new BinaryFormatter();

            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);
        }

        protected async Task<Packet> ReadTCP()
        {
            // Read until a packet size is encountered
            byte[] sizeBuffer = new byte[sizeof(uint)];
            int bytesRead = await _tcpClient.GetStream().ReadAsync(sizeBuffer);
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
            if (bytesRead <= 0)
            {
                return null;
            }
            else if (bytesRead < packetBuffer.Length)
            {
                throw new ClientException("Invalid packetBuffer, read " + bytesRead + " bytes when it should be " + packetBuffer.Length);
            }

            // Deserialise Packet
            MemoryStream ms = new MemoryStream(packetBuffer);
            return _formatter.Deserialize(ms) as Packet;
        }

        protected async Task WriteTCP(Packet packet)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                byte[] buffer = ms.GetBuffer();

                // Send Packet Size + Packet
                await _tcpClient.GetStream().WriteAsync(BitConverter.GetBytes((uint)buffer.Length));
                await _tcpClient.GetStream().WriteAsync(buffer);

                // Flush Stream
                await _tcpClient.GetStream().FlushAsync();
            }
            finally
            {
                _tcpWriteAsyncLock.Release();
            }
        }
    }
}
