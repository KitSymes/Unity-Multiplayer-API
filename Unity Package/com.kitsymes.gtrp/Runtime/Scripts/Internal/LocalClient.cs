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

            Debug.Log("Connected");

            await WriteTCP(new PacketConnect() { udpEndPoint = _udpClient.Client.LocalEndPoint as IPEndPoint });
            return true;
        }

        public void Stop()
        {
            _running = false;
            _tcpClient.Close();
            _udpClient?.Close();
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
                    Debug.Log("Recieved " + packet.GetType());
                    _networkManager.ClientPacketReceived(packet);
                }
                catch (IOException)
                {
                    Debug.Log("CLIENT: TCP Closed");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogError("CLIENT: Error vvvv");
                    Debug.LogException(ex);
                    break;
                }
            }
            if (_running)
            {
                Debug.LogWarning("TCP Closed whilst client is still running");
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
                    Packet packet = PacketFormatter.Deserialise(result.Buffer);
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
