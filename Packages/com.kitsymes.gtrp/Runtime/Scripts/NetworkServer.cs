using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP
{
    public class NetworkServer : MonoBehaviour
    {
        // Shared
        //public Scene _offlineScene;
        //public Scene _onlineScene;
        public static NetworkServer Instance { get; private set; }
        public string ip = "127.0.0.1";
        public int port = 25565;
        //private UdpClient _udpClient;
        //private Dictionary<uint, Action<Packet>> _handlers = new Dictionary<uint, Action<Packet>>();

        // Server Only
        private Dictionary<Type, Action<Packet>> _serverHandlers = new();
        private bool _serverRunning = false;
        private int _clientCount;
        private Dictionary<int, Client> _clients;
        private TcpListener _tcpListener;
        private UdpClient _udpListener;

        // Client Only
        private Dictionary<Type, Action<Packet>> _clientHandlers = new();
        private LocalClient _localClient;

        void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(this);
            else
                Instance = this;

            // Poke the PacketRegister to make sure it's loaded on start
            //_ = PacketRegister.Instance;
        }

        void OnDestroy()
        {
            if (Instance != null && Instance == this)
                Instance = null;
        }

        public void TestPacketMethod(PacketSpawnObject packet)
        {
            Debug.Log("pso recieved");
        }

        #region Server Methods
        public void ServerStart()
        {
            if (_serverRunning)
                return;

            _serverRunning = true;
            _clientCount = 0;
            _clients = new Dictionary<int, Client>();
            DontDestroyOnLoad(gameObject);

            _tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
            _tcpListener.Start();
            AcceptTCPClients();

            _udpListener = new UdpClient(port);
            UdpListen();

            Debug.Log("Server Started");
        }

        public void ServerStop()
        {
            if (!_serverRunning)
                return;
            _serverRunning = false;

            foreach (Client c in _clients.Values)
            {
                c.Stop();
            }
            _clients.Clear();

            _tcpListener.Stop();
            _udpListener.Close();

            _clientHandlers.Clear();

            Debug.Log("Server Stopped");
        }

        public bool IsServerRunning()
        {
            return _serverRunning;
        }

        private async void AcceptTCPClients()
        {
            while (_serverRunning)
            {
                Task<TcpClient> task = _tcpListener.AcceptTcpClientAsync();
                try
                {
                    await task;
                    // TODO
                    // Increment _clientCount first, because we want to count clients normally, as ID 0 represents the Server (For things such as ownerID)
                    _clientCount++;
                    Client c = new Client(_clientCount, task.Result);
                    _clients.Add(_clientCount, c);
                    Debug.Log("Accepted");
                    c.SendTCP(new PacketSpawnObject());
                    c.SendTCP(new PacketDespawnObject());
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("Server closed so stopping accepting TCP Clients");
                }
            }
        }

        private async void UdpListen()
        {
            while (_serverRunning)
            {
                try
                {
                    Task<UdpReceiveResult> task = _udpListener.ReceiveAsync();
                    await task;
                    UdpReceiveResult result = task.Result;
                    Debug.Log("Udp Listened");
                }
                catch (ObjectDisposedException)
                {
                    Debug.Log("Server closed so stopping listening to UDP");
                }
            }
        }

        // Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        public void RegisterServerPacketHandler<T>(Action<T> handler) where T : Packet
        {
            Action<Packet> wrapper = packet => handler(packet as T);
            if (_serverHandlers.ContainsKey(typeof(T)))
                _serverHandlers[typeof(T)] += wrapper;
            else
                _serverHandlers.Add(typeof(T), wrapper);
        }

        public void ServerPacketReceived(Packet packet)
        {
            if (_serverHandlers.ContainsKey(packet.GetType()) && _serverHandlers[packet.GetType()] != null)
                _serverHandlers[packet.GetType()].Invoke(packet);
        }
        #endregion

        #region Client Methods
        public async void ClientStart()
        {
            if (_localClient != null || IsClientRunning())
                return;

            _localClient = new LocalClient(ip, port);
            bool connected = await _localClient.Connect();
            if (connected)
                Debug.Log("Client Started");
            else
            {
                _localClient.Stop();
                _localClient = null;
            }

            RegisterClientPacketHandler<PacketSpawnObject>(TestPacketMethod);
        }

        public void ClientStop()
        {
            if (!IsClientRunning())
                return;

            _localClient.Stop();
            _localClient = null;

            _clientHandlers.Clear();
            Debug.Log("Client Stopped");
        }

        public bool IsClientRunning()
        {
            return _localClient != null && _localClient.IsRunning();
        }

        // Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        public void RegisterClientPacketHandler<T>(Action<T> handler) where T : Packet
        {
            Action<Packet> wrapper = packet => handler(packet as T);
            if (_clientHandlers.ContainsKey(typeof(T)))
                _clientHandlers[typeof(T)] += wrapper;
            else
                _clientHandlers.Add(typeof(T), wrapper);
        }

        public void ClientPacketReceived(Packet packet)
        {
            if (_clientHandlers.ContainsKey(packet.GetType()) && _clientHandlers[packet.GetType()] != null)
                _clientHandlers[packet.GetType()].Invoke(packet);
        }
        #endregion
    }
}
