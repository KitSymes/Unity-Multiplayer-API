using KitSymes.GTRP.Internal;
using KitSymes.GTRP.MonoBehaviours;
using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace KitSymes.GTRP
{
    [Serializable]
    public class NetworkManager
    {
        // Shared
        private static NetworkManager _instance;

        public string _offlineScene;
        public string _onlineScene;

        public List<NetworkObject> _spawnableObjects;
        private uint _spawnedObjectsCount;
        public Dictionary<uint, NetworkObject> _spawnedObjects;

        // Server Only
        private Dictionary<Type, Action<Packet>> _serverHandlers = new();
        private bool _serverRunning = false;
        private uint _clientCount;
        private Dictionary<uint, Client> _clients;
        private TcpListener _tcpListener;
        private UdpClient _udpClient;

        // Client Only
        private Dictionary<Type, Action<Packet>> _clientHandlers = new();
        private LocalClient _localClient;

        void SharedStart()
        {
            if (_instance == null)
                _instance = this;

            if (_spawnableObjects == null)
                _spawnableObjects = new List<NetworkObject>();
            _spawnedObjectsCount = 0;
            _spawnedObjects = new Dictionary<uint, NetworkObject>();

            // Only load the scene if has not already loaded it because it's both.
            if (!IsServerRunning() || !IsClientRunning())
            {
                SceneManager.LoadScene(_onlineScene);
            }
        }

        void SharedStop()
        {
            if (_instance != null && _instance == this && !IsClientRunning() && !IsServerRunning())
                _instance = null;

            _spawnedObjects.Clear();

            if (!IsServerRunning() && !IsClientRunning())
                SceneManager.LoadScene(_offlineScene);
        }

        void Update()
        {
            if (IsServerRunning())
            {
                List<Packet> tcpPackets = new List<Packet>();
                List<Packet> udpPackets = new List<Packet>();
                foreach (NetworkObject networkObject in _spawnedObjects.Values)
                {
                    tcpPackets.AddRange(networkObject.GetTCPPackets());
                    udpPackets.AddRange(networkObject.GetUDPPackets());
                    networkObject.ClearPackets();
                }
                SendToAll(tcpPackets.ToArray());
                Broadcast(udpPackets.ToArray());
            }
        }

        #region Server Methods
        public void ServerStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_serverRunning)
                return;

            _serverRunning = true;
            _clientCount = 0;
            _clients = new Dictionary<uint, Client>();

            _tcpListener = new TcpListener(System.Net.IPAddress.Any, port);
            _tcpListener.Start();
            _ = AcceptTCPClients();

            _udpClient = new UdpClient(port);
            _ = UdpListen();

            SharedStart();

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
            _udpClient.Close();

            _clientHandlers.Clear();

            SharedStop();

            Debug.Log("Server Stopped");
        }

        public bool IsServerRunning()
        {
            return _serverRunning;
        }

        private async Task AcceptTCPClients()
        {
            while (_serverRunning)
            {
                Task<TcpClient> task = _tcpListener.AcceptTcpClientAsync();
                try
                {
                    await task;
                    // Increment _clientCount first, because we want to count clients normally, as ID 0 represents the Server (For things such as ownerID)
                    _clientCount++;
                    Client c = new Client(_clientCount, task.Result);
                    _clients.Add(_clientCount, c);
                    //TODO send them all spawned objects
                    Debug.Log("Accepted a Connection");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
            Debug.Log("Server closed so stopping accepting TCP Clients");
        }

        private async Task UdpListen()
        {
            while (_serverRunning)
            {
                try
                {
                    Task<UdpReceiveResult> task = _udpClient.ReceiveAsync();
                    await task;
                    UdpReceiveResult result = task.Result;
                    Debug.Log("Received Udp");
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("Server closed so stopping listening to UDP");
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

        private void SendTo(uint clientID, Packet packet)
        {
            if (!_clients.ContainsKey(clientID))
            {
                Debug.LogError("Attempted to send packet to invalid clientID " + clientID);
                return;
            }
            _clients[clientID].SendTCP(packet);
        }

        private void SendToAll(params Packet[] packets)
        {
            foreach (Client client in _clients.Values)
                client.SendTCP(packets);
        }

        private void Broadcast(params Packet[] packets)
        {
            _udpClient.SendAsync();
        }

        public void Spawn(NetworkObject obj)
        {
            if (!IsServerRunning())
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no server running");
                return;
            }

            if (obj.IsSpawned())
            {
                Debug.LogError("Tried to Spawn " + obj.name + " again");
                return;
            }

            _spawnedObjects[_spawnedObjectsCount] = obj;
            obj.Spawn(_spawnedObjectsCount, 0);
            _spawnedObjectsCount++;
            ExecuteEvents.Execute<INetworkMessageTarget>(obj.gameObject, null, (x, y) => x.OnServerStart());

            SendToAll(new PacketSpawnObject
            {
                prefabID = obj.GetPrefabID(),
                objectNetworkID = obj.GetNetworkID(),
                ownerNetworkID = obj.GetOwnerID(),
                positionX = obj.transform.position.x,
                positionY = obj.transform.position.y,
                positionZ = obj.transform.position.z,
                rotationX = obj.transform.rotation.x,
                rotationY = obj.transform.rotation.y,
                rotationZ = obj.transform.rotation.z,
                rotationW = obj.transform.rotation.w,
                localScaleX = obj.transform.localScale.x,
                localScaleY = obj.transform.localScale.y,
                localScaleZ = obj.transform.localScale.z,
            });
        }

        public static void Spawn(GameObject obj)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no server running");
                return;
            }

            NetworkObject networkObject = obj.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " but it does not have a NetworkObject component");
                return;
            }

            _instance.Spawn(networkObject);
        }
        #endregion

        #region Client Methods
        public async Task ClientStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_localClient != null || IsClientRunning())
                throw new ClientException("Client is already running");

            _localClient = new LocalClient(this, ip, port);
            bool connected = await _localClient.Connect();
            if (connected)
                Debug.Log("Client Started");
            else
            {
                _localClient.Stop();
                _localClient = null;
                throw new ClientException("Client failed to connect");
            }

            SharedStart();
            RegisterClientPacketHandler<PacketSpawnObject>(ClientPacketSpawnObjectReceived);
        }

        public void ClientStop()
        {
            if (!IsClientRunning())
                return;

            _localClient.Stop();
            _localClient = null;

            _clientHandlers.Clear();

            SharedStop();
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

        #region Client Handlers
        public void ClientPacketSpawnObjectReceived(PacketSpawnObject packet)
        {
            // Validate Packet
            if (packet.prefabID >= _spawnableObjects.Count)
                return;
            if (_spawnedObjects.ContainsKey(packet.objectNetworkID))
                return;

            // Do not spawn duplicate if this client is host
            if (IsServerRunning())
                return;

            GameObject newObj = UnityEngine.Object.Instantiate(_spawnableObjects[(int)packet.prefabID].gameObject);
            newObj.transform.SetPositionAndRotation(packet.GetPosition(), packet.GetRotation());
            newObj.transform.localScale = packet.GetScale();

            NetworkObject networkObject = newObj.GetComponent<NetworkObject>();
            _spawnedObjects[packet.objectNetworkID] = networkObject;
            networkObject.Spawn(packet.objectNetworkID, packet.ownerNetworkID);
        }
        #endregion

        #region Setters
        public void SetOfflineScene(string offlineScene)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _offlineScene = offlineScene;
        }

        public void SetOnlineScene(string onlineScene)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _onlineScene = onlineScene;
        }

        public void SetSpawnableObjects(List<NetworkObject> spawnableObjects)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _spawnableObjects = spawnableObjects;
        }
        #endregion
    }
}
