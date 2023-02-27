using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class LocalClient
    {
        private int _id;

        private NetworkManager _networkManager;
        private string _ip;
        private int _port;

        private bool _running = false;

        private TcpClient _tcpClient;
        private UdpClient _udpClient;

        private BinaryReader _reader;
        private BinaryWriter _writer;
        private BinaryFormatter _formatter;

        public LocalClient(NetworkManager networkManager, string ip, int port)
        {
            _id = -1;
            _networkManager = networkManager;
            _ip = ip;
            _port = port;

            _tcpClient = new TcpClient();
            _udpClient = new UdpClient();
        }

        public async Task<bool> Connect()
        {
            IPAddress address;
            if (!IPAddress.TryParse(_ip, out address))
                return false;

            Task task = _tcpClient.ConnectAsync(address, _port);

            try
            {
                await task;
            }
            catch (SocketException)
            {
                Debug.LogError("Client could not connect");
                return false;
            }

            _reader = new BinaryReader(_tcpClient.GetStream());
            _writer = new BinaryWriter(_tcpClient.GetStream());
            _formatter = new BinaryFormatter();

            _udpClient.Connect(address, _port);

            _running = true;

            ReceiveTcp();
            ReceiveUdp();
            Debug.Log("Connected");
            return true;
        }

        public void Stop()
        {
            _running = false;
            _reader?.Close();
            _writer?.Close();
            _tcpClient.Close();
            _udpClient?.Close();
        }

        public async void ReceiveTcp()
        {
            while (_running)
            {
                try
                {
                    // Read until a packet size is encountered
                    byte[] sizeBuffer = new byte[sizeof(int)];
                    int bytesRead = await _tcpClient.GetStream().ReadAsync(sizeBuffer);
                    if (bytesRead < sizeBuffer.Length)
                    {
                        Debug.LogError("Invalid sizeBuffer, read " + bytesRead + " bytes when it should be " + sizeBuffer.Length);
                        continue;
                    }
                    int bufferSize = BitConverter.ToInt32(sizeBuffer);

                    // Try and read the whole packet
                    byte[] packetBuffer = new byte[bufferSize];
                    bytesRead = await _tcpClient.GetStream().ReadAsync(packetBuffer);
                    if (bytesRead < packetBuffer.Length)
                    {
                        Debug.LogError("Invalid packetBuffer, read " + bytesRead + " bytes when it should be " + packetBuffer.Length);
                        continue;
                    }

                    // Deserialise Packet
                    MemoryStream ms = new MemoryStream(packetBuffer);
                    Packet packet = _formatter.Deserialize(ms) as Packet;
                    _networkManager.ClientPacketReceived(packet);
                }
                catch (IOException)
                {
                    //Debug.Log("Client closed so stopping receiving TCP");
                    // TODO Server potentially closed, so add a check and handle accordingly
                }
            }
        }

        public async void ReceiveUdp()
        {
            while (_running)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("Client closed so stopping Receiving UDP");
                }
            }
        }

        public int GetID() { return _id; }

        public bool IsRunning() { return _running; }
    }
}
