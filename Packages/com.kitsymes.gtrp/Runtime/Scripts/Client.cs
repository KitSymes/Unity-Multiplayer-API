using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace KitSymes.GTRP.Internal
{
    public class Client
    {
        // 0 is Server, so this ID will start at 1
        private int _id;

        private TcpClient _tcpClient;
        private SemaphoreSlim _tcpWriteAsyncLock;

        private BinaryReader _reader;
        private BinaryWriter _writer;
        private BinaryFormatter _formatter;

        public Client(int id, TcpClient tcp)
        {
            _id = id;
            _tcpClient = tcp;

            _reader = new BinaryReader(_tcpClient.GetStream());
            _writer = new BinaryWriter(_tcpClient.GetStream());
            _formatter = new BinaryFormatter();
            _tcpWriteAsyncLock = new SemaphoreSlim(1, 1);
        }

        public void Stop()
        {
            _reader?.Close();
            _writer?.Close();
            _tcpClient.Close();
        }

        public async void SendTCP(Packet packet)
        {
            await _tcpWriteAsyncLock.WaitAsync();

            try
            {
                // Prepare Packet
                MemoryStream ms = new MemoryStream();
                _formatter.Serialize(ms, packet);
                byte[] buffer = ms.GetBuffer();

                // Send Packet Size + Packet
                await _tcpClient.GetStream().WriteAsync(BitConverter.GetBytes(buffer.Length));
                await _tcpClient.GetStream().WriteAsync(buffer);

                // Flush Stream
                await _tcpClient.GetStream().FlushAsync();
            }
            finally
            {
                _tcpWriteAsyncLock.Release();
            }
        }

        public int GetID() { return _id; }
    }
}
