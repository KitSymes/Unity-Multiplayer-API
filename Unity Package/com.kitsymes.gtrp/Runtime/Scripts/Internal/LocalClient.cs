using KitSymes.GTRP.Packets;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP.Internal
{
    public class LocalClient : Client
    {
        private NetworkManager _networkManager;
        private string _ip;
        private int _port;

        private bool _running = false;

        private UdpClient _udpClient;

        public LocalClient(NetworkManager networkManager, string ip, int port)
        {
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

            try
            {
                await _tcpClient.ConnectAsync(address, _port);
            }
            catch (SocketException)
            {
                Debug.LogError("Client could not connect");
                return false;
            }

            _udpClient.Connect(address, _port);

            _running = true;

            ReceiveTcp();
            ReceiveUdp();

            Debug.Log("Connected");

            SendTCP(new PacketConnect() { udpEndPoint = _udpClient.Client.LocalEndPoint });
            return true;
        }

        public void Stop()
        {
            _running = false;
            _tcpClient.Close();
            _udpClient?.Close();
        }

        public async void SendTCP(Packet packet)
        {
            await WriteTCP(packet);
        }

        public async void ReceiveTcp()
        {
            while (_running)
            {
                try
                {
                    Packet packet = await ReadTCP();
                    if (packet == null)
                        break;

                    _networkManager.ClientPacketReceived(packet);
                }
                catch (IOException)
                {
                    Debug.Log("TCP IO Exception");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("TCP Disposed");
                    break;
                }
                catch (ClientException ex)
                {
                    Debug.LogException(ex);
                }
            }
            if (_running)
            {
                Debug.LogError("TCP Closed whilst client is still running");
                _networkManager.ClientStop();
            }
            Debug.Log("Client stopping receiving TCP");
        }

        public async void ReceiveUdp()
        {
            while (_running)
            {
                try
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();

                    // Deserialise Packet
                    MemoryStream ms = new MemoryStream(result.Buffer);
                    Packet packet = _formatter.Deserialize(ms) as Packet;
                    _networkManager.ClientPacketReceived(packet);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
            Debug.Log("Client stopping Receiving UDP");
        }

        public bool IsRunning() { return _running; }
    }
}
