using KitSymes.GTRP.Internal;
using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KitSymes.GTRP
{
    /// <summary>
    /// The NetworkManager controls both the Client and Server.
    /// Certain methods can be called through Static functions once a Client or Server has been started.
    /// The default implementation is controlled through <see cref="Components.NetworkManagerComponent"/>, but you can copy that component to make your own.
    /// </summary>
    [Serializable]
    public sealed class NetworkManager
    {
        // Delegates and Events
        /// <summary>
        /// The Delegate for NetworkManager events.
        /// </summary>
        public delegate void EventSubscriber();
        /// <summary>
        /// The Event called when the Server Starts.
        /// </summary>
        public event EventSubscriber OnServerStart;
        /// <summary>
        /// The Event called when the Server Stops.
        /// </summary>
        public event EventSubscriber OnServerStop;
        /// <summary>
        /// The Event called when the Client Starts.
        /// </summary>
        public event EventSubscriber OnClientStart;
        /// <summary>
        /// The Event called when the Client Stops.
        /// </summary>
        public event EventSubscriber OnClientStop;
        /// <summary>
        /// The Delegate for Player related NetworkManager events.
        /// </summary>
        /// <param name="key"></param>
        public delegate void ClientEventSubscriber(uint key);
        /// <summary>
        /// The event called on the Server when a Player connects.
        /// </summary>
        public event ClientEventSubscriber ServerOnPlayerConnect;
        /// <summary>
        /// The event called on the Server when a Player disconnects.
        /// </summary>
        public event ClientEventSubscriber ServerOnPlayerDisconnect;

        // Shared
        private static NetworkManager _instance;

        /// <summary>
        /// The List of all spawnable Prefabs (which must have a NetworkObject attached).
        /// </summary>
        private List<NetworkObject> _spawnableObjects = new List<NetworkObject>();
        /// <summary>
        /// The runtime Dictionary of all spawned Prefabs, mapped to their unique spawned object ID.
        /// </summary>
        private Dictionary<uint, NetworkObject> _spawnedObjects = new Dictionary<uint, NetworkObject>();
        /// <summary>
        /// The number of times per second the game ticks at. Changable in the Insepctor. Should be the same for both Client and Server.
        /// </summary>
        [SerializeField, Tooltip("Change the number of ticks per second")]
        private int _tickRate = 20;
        /// <summary>
        /// The time since the game last ticked.
        /// </summary>
        float _timeSinceLastTick = 0;

        // Server Only
        private Dictionary<Type, Action<ServerSideClient, Packet>> _serverHandlers = new();
        private bool _serverRunning = false;
        private uint _serverClientCount;
        private Dictionary<uint, ServerSideClient> _serverSideClients;
        private TcpListener _serverTCPListener;
        private UdpClient _serverUDPClient;
        private uint _serverSpawnedObjectsCount;
        private NetworkObject _serverPlayerPrefab;
        private Dictionary<uint, NetworkObject> _serverPlayers = new();

        // Client Only
        private Dictionary<Type, Action<Packet>> _clientHandlers = new();
        private LocalClient _clientLocalClient;
        private uint _clientID;


#if UNITY_EDITOR
        // Debug Variables

        // Variables to display the number of bytes read per second
        public int bytesRead = 0;
        [SerializeField]
        private Text _bytesReadText;
        private float _timeSinceLastBytesReadUpdate = 0.0f;
#endif

        /// <summary>
        /// Called by both <see cref="ServerStart(int)"/> and <see cref="ClientStart(string, int)"/>.
        /// </summary>
        void SharedStart()
        {
            if (_instance == null)
                _instance = this;

            if (!IsServerRunning() || !IsClientRunning())
            {

            }
        }
        /// <summary>
        /// Called by both <see cref="ServerStop"/> and <see cref="ClientStop"/>.
        /// </summary>
        void SharedStop()
        {
            if (_instance != null && _instance == this && !IsClientRunning() && !IsServerRunning())
                _instance = null;

            if (!IsServerRunning() && !IsClientRunning())
            {
                _spawnedObjects.Clear();
            }
        }

        /// <summary>
        /// Handles the game ticking.
        /// Called by <see cref="GTRP.Components.NetworkManagerComponent.LateUpdate"/>.
        /// </summary>
        public void LateUpdate()
        {
#if UNITY_EDITOR
            _timeSinceLastBytesReadUpdate += Time.deltaTime;
            if (_timeSinceLastBytesReadUpdate > 1.0f)
            {
                _timeSinceLastBytesReadUpdate = 0.0f;
                if (_bytesReadText != null)
                    _bytesReadText.text = bytesRead + " bytes/s";
                bytesRead = 0;
            }
#endif

            // Add the time since last frame to the time since last tick
            _timeSinceLastTick += Time.deltaTime;
            // Check to see if the game should tick this frame
            if (_timeSinceLastTick < 1.0f / _tickRate)
                return;
            _timeSinceLastTick = 0.0f;

            List<Packet> tcpPackets = new List<Packet>();
            List<Packet> udpPackets = new List<Packet>();
            // Tick all the objects
            foreach (NetworkObject networkObject in _spawnedObjects.Values)
            {
                networkObject.Tick();
                // If they have any packets they want to send to the other side, grab them
                tcpPackets.AddRange(networkObject.GetTCPPackets());
                tcpPackets.AddRange(networkObject.GetAllDynamicSyncPackets());
                udpPackets.AddRange(networkObject.GetUDPPackets());
                networkObject.ClearPackets();
            }

            // If the Server is running, send to Clients
            if (IsServerRunning())
            {
                SendToAll(tcpPackets.ToArray());
                Broadcast(udpPackets.ToArray());
            }
            // If the Client is running, send to the Server
            else if (IsClientRunning())
            {
                SendToServerTCP(tcpPackets.ToArray());
                SendToServerUDP(udpPackets.ToArray());
            }
        }

        /// <summary>
        /// Get the NetworkManager instance.
        /// </summary>
        /// <returns>null if neither a Client or Server is running, or the NetworkManager instance.</returns>
        public static NetworkManager GetInstance()
        {
            return _instance;
        }

        #region Server Methods
        /// <summary>
        /// Start the Server.
        /// Pass in the Port to bind to, or leave blank for 25565.
        /// </summary>
        /// <param name="port">The Port to bind to.</param>
        /// <returns>True if the Server started sucessfully.</returns>
        public bool ServerStart(int port = 25565)
        {
            if (_serverRunning)
                return false;

            _serverTCPListener = new TcpListener(System.Net.IPAddress.Any, port);
            try
            {
                _serverTCPListener.Start();
            }
            catch (SocketException)
            {
                Debug.LogError("Could not start Server");
                return false;
            }

            _serverRunning = true;
            _serverClientCount = 0;
            _serverSideClients = new Dictionary<uint, ServerSideClient>();
            _serverSpawnedObjectsCount = 0;

            _ = AcceptTCPClients();

            _serverUDPClient = new UdpClient(port);
            _ = UdpListen();

            SharedStart();
            RegisterServerPacketHandler<PacketPing>(OnServerPacketPingReceived);
            //RegisterServerPacketHandler<PacketConnect>(OnServerPacketConnectReceived);
            RegisterServerPacketHandler<PacketServerRPC>(OnServerPacketServerRPCReceived);
            RegisterServerPacketHandler<PacketNetworkTransformSync>(OnServerPacketTargetedReceived);

            OnServerStart?.Invoke();
            return true;
        }
        /// <summary>
        /// Stop the Server.
        /// Clears handlers and closes Clients.
        /// </summary>
        public void ServerStop()
        {
            if (!_serverRunning)
                return;
            _serverRunning = false;

            if (_serverSideClients != null)
            {
                foreach (ServerSideClient c in _serverSideClients.Values)
                {
                    c.Stop();
                }
                _serverSideClients.Clear();
            }

            _serverTCPListener.Stop();
            _serverUDPClient.Close();

            _clientHandlers.Clear();

            SharedStop();

            OnServerStop?.Invoke();
        }
        /// <summary>
        /// Check if the Server is running.
        /// </summary>
        /// <returns></returns>
        public bool IsServerRunning()
        {
            return _serverRunning;
        }

        private async Task AcceptTCPClients()
        {
            while (_serverRunning)
            {
                Task<TcpClient> task = _serverTCPListener.AcceptTcpClientAsync();
                try
                {
                    await task;

                    // Increment _clientCount first, because we want to count clients normally, as ID 0 represents the Server (For things such as ownerID)
                    _serverClientCount++;
                    ServerSideClient c = new ServerSideClient(this, _serverClientCount, task.Result);
                    _serverSideClients.Add(c.GetID(), c);

                    if (_serverPlayerPrefab != null)
                    {
                        NetworkObject playerNetworkObject = UnityEngine.Object.Instantiate(_serverPlayerPrefab);
                        playerNetworkObject.gameObject.name = "Player " + c.GetID();
                        playerNetworkObject.ChangeOwnership(c.GetID());
                        Spawn(playerNetworkObject);
                        _serverPlayers.Add(c.GetID(), playerNetworkObject);
                    }

                    ServerOnPlayerConnect?.Invoke(c.GetID());
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
            //Debug.Log("Server closed so stopping accepting TCP Clients");
        }
        private async Task UdpListen()
        {
            while (_serverRunning)
            {
                try
                {
                    UdpReceiveResult result = await _serverUDPClient.ReceiveAsync();
#if UNITY_EDITOR
                    bytesRead += result.Buffer.Length;
#endif

                    foreach (ServerSideClient client in _serverSideClients.Values)
                    {
                        if (result.RemoteEndPoint.Equals(client.GetUdpEndPoint()))
                        {
                            try
                            {
                                // Deserialise Packet
                                Packet packet = PacketFormatter.Deserialise(result.Buffer);
                                ServerPacketReceived(client, packet);
                            }
                            catch (Exception ex)
                            {
                                Debug.LogWarning(ex);
                            }
                            break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    //Debug.Log("Server closed so stopping listening to UDP");
                }
            }
        }

        /// <summary>
        /// Call to forcefully disconnect the Player of a given network ID.
        /// Also called by the API when a Player disconnects internally/naturally in <see cref="ServerSideClient.ReceiveTcp"/>.
        /// </summary>
        /// <param name="id"></param>
        public void Disconnect(uint id)
        {
            if (!_serverSideClients.ContainsKey(id))
                return;

            Debug.Log("SERVER: [" + id + "] Client disconnecting");

            ServerOnPlayerDisconnect?.Invoke(id);

            ServerSideClient client = _serverSideClients[id];
            client.Stop();
            _serverSideClients.Remove(id);

            if (_serverPlayers.ContainsKey(id))
            {
                NetworkObject networkObject = _serverPlayers[id];
                UnityEngine.Object.Destroy(networkObject.gameObject);
                _serverPlayers.Remove(id);
            }
        }

        /// <summary>
        /// Register a Function to be called when the given <see cref="Packet"/> is received on the Server.
        /// Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        /// </summary>
        /// <typeparam name="T">The <see cref="Packet"/> to listen for.</typeparam>
        /// <param name="handler">The Function to call when the <see cref="Packet"/> of type <typeparamref name="T"/> is received.</param>
        public void RegisterServerPacketHandler<T>(Action<ServerSideClient, T> handler) where T : Packet
        {
            Action<ServerSideClient, Packet> wrapper = (sender, packet) => handler(sender, packet as T);
            if (_serverHandlers.ContainsKey(typeof(T)))
                _serverHandlers[typeof(T)] += wrapper;
            else
                _serverHandlers.Add(typeof(T), wrapper);
        }
        internal void ServerPacketReceived(ServerSideClient sender, Packet packet)
        {
            if (_serverHandlers.ContainsKey(packet.GetType()) && _serverHandlers[packet.GetType()] != null)
                _serverHandlers[packet.GetType()].Invoke(sender, packet);
        }

        /// <summary>
        /// Send packets to a specific Client over TCP
        /// </summary>
        /// <param name="clientID">The target Client</param>
        /// <param name="packets">The packets to send</param>
        public void SendTo(uint clientID, params Packet[] packets)
        {
            if (!_serverSideClients.ContainsKey(clientID))
            {
                Debug.LogError("Attempted to send packet to invalid clientID " + clientID);
                return;
            }

            SendTo(_serverSideClients[clientID], packets);
        }
        /// <summary>
        /// Send packets to a specific Client over TCP
        /// </summary>
        /// <param name="client">The target Client</param>
        /// <param name="packets">The packets to send</param>
        public void SendTo(ServerSideClient client, params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Send Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            _ = client.WriteTCP(packetBuffer.ToArray());
        }
        /// <summary>
        /// Send packets to all Clients over TCP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToAll(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            foreach (ServerSideClient client in _serverSideClients.Values)
                _ = client.WriteTCP(packetBuffer.ToArray());
        }

        /// <summary>
        /// Send packets to all Clients over UDP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void Broadcast(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            //List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] bytes = PacketFormatter.Serialise(packet);

                foreach (ServerSideClient client in _serverSideClients.Values)
                    if (client.GetUdpEndPoint() != null)
                        _serverUDPClient.SendAsync(bytes, bytes.Length, client.GetUdpEndPoint());
            }
        }

        /// <summary>
        /// Get the <see cref="NetworkObject"/> of a Player with the given network ID.
        /// </summary>
        /// <param name="id"></param>
        /// <returns><c>null</c> or the <see cref="NetworkObject"/> the Player owns.</returns>
        public NetworkObject GetPlayerObject(uint id)
        {
            if (!_serverPlayers.ContainsKey(id))
                return null;
            return _serverPlayers[id];
        }

        // Static Functions
        /// <summary>
        /// Statically check if the Server is running.
        /// </summary>
        /// <returns>True if the Server is running.</returns>
        public static bool IsServer()
        {
            if (_instance == null)
                return false;

            return _instance.IsServerRunning();
        }

        /// <summary>
        /// Spawn a <see cref="NetworkObject"/>.
        /// Spawning an object synchronises it to Clients. Before it is spawned, it only exists on the Server.
        /// </summary>
        /// <param name="obj">The <see cref="NetworkObject"/> to spawn.</param>
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

            _spawnedObjects[_serverSpawnedObjectsCount] = obj;
            obj.Spawn(_serverSpawnedObjectsCount, obj.GetOwnerID(), obj.HasAuthority());
            _serverSpawnedObjectsCount++;
            ExecuteEvents.Execute<INetworkMessageTarget>(obj.gameObject, null, (x, y) => x.OnServerStart());

            List<Packet> packets = new List<Packet>();
            packets.Add(obj.GetSpawnPacket());
            packets.AddRange(obj.GetAllFullSyncPackets());

            SendToAll(packets.ToArray());
        }
        /// <summary>
        /// Spawn a GameObject.
        /// Must have a <see cref="NetworkObject"/> attached and be registered in the <see cref="_spawnableObjects"/> list.
        /// </summary>
        /// <param name="obj">The GameObject to spawn. Must have a <see cref="NetworkObject"/> attached and be registered in the <see cref="_spawnableObjects"/> list.</param>
        public static void Spawn(GameObject obj)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Spawn " + obj.name + " with no instance running");
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

        /// <summary>
        /// Despawn a <see cref="NetworkObject"/>. Does not delete it.
        /// This informs Clients to despawn it too (which does delete it).
        /// </summary>
        /// <param name="id">The <see cref="NetworkObject"/>'s network ID for validation purposes.</param>
        /// <param name="networkObject">The <see cref="NetworkObject"/> to despawn.</param>
        public void Despawn(uint id, NetworkObject networkObject)
        {
            // Validate
            if (!IsServerRunning())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " with no server running");
                return;
            }

            if (id != networkObject.GetNetworkID())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it had an ID mismatch");
                return;
            }

            if (!_spawnedObjects.ContainsKey(id))
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it does not exist");
                return;
            }

            if (_spawnedObjects[id] != networkObject)
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " but it does not match its ID");
                return;
            }

            if (!networkObject.IsSpawned())
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " when it wasn't spawned");
                return;
            }

            _spawnedObjects.Remove(id);
            SendToAll(new PacketDespawnObject() { objectNetworkID = id });
        }
        /// <summary>
        /// Despawn a <see cref="NetworkObject"/>. Does not delete it.
        /// This informs Clients to despawn it too (which does delete it).
        /// </summary>
        /// <param name="networkObject">The <see cref="NetworkObject"/> to despawn.</param>
        public static void Despawn(NetworkObject networkObject)
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to Despawn " + networkObject.name + " with no instance running");
                return;
            }

            _instance.Despawn(networkObject.GetNetworkID(), networkObject);
        }
        #endregion

        #region Server Handlers
        private void OnServerPacketPingReceived(ServerSideClient sender, PacketPing packet)
        {
            Debug.Log($"[{sender.GetID()}] Ping");
            _ = sender.WriteTCP(new PacketPong());
        }

        private void OnServerPacketServerRPCReceived(ServerSideClient sender, PacketServerRPC packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (networkObject.GetOwnerID() != sender.GetID())
                return;
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            networkBehaviour.OnPacketServerRPCReceive(packet);
        }

        private void OnServerPacketTargetedReceived(ServerSideClient sender, PacketTargeted packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.target))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.target];
            if (networkObject.GetOwnerID() != sender.GetID())
                return;
            if (!networkObject.HasAuthority())
                return;

            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null,
                (x, y) => x.OnPacketReceive(packet));
        }
        #endregion

        #region Client Methods
        /// <summary>
        /// Start the Client. Pass in the IPv4 Address and Port to connet to, or leave blank for 127.0.0.1:25565.
        /// </summary>
        /// <param name="ip">The IPv4 address to connect to. Default 127.0.0.1</param>
        /// <param name="port">The Port to connect to. Default 25565</param>
        /// <returns></returns>
        /// <exception cref="ClientException"></exception>
        public async Task ClientStart(string ip = "127.0.0.1", int port = 25565)
        {
            if (_clientLocalClient != null || IsClientRunning())
                throw new ClientException("Client is already running");

            _clientLocalClient = new LocalClient(this, ip, port);
            bool connected = await _clientLocalClient.Connect();
            if (!connected)
            {
                _clientLocalClient.Stop();
                _clientLocalClient = null;
                throw new ClientException("Client failed to connect");
            }

            SharedStart();
            RegisterClientPacketHandler<PacketServerInfo>(OnClientPacketServerInfoReceived);
            RegisterClientPacketHandler<PacketPong>(OnClientPacketPongReceived);
            RegisterClientPacketHandler<PacketSpawnObject>(OnClientPacketSpawnObjectReceived);
            RegisterClientPacketHandler<PacketDespawnObject>(OnClientPacketDespawnObjectReceived);
            RegisterClientPacketHandler<PacketNetworkTransformSync>(OnClientPacketTargetedReceived);
            RegisterClientPacketHandler<PacketNetworkBehaviourSync>(OnClientPacketNetworkBehaviourSyncReceived);
            RegisterClientPacketHandler<PacketClientRPC>(OnClientPacketClientRPCReceived);
            RegisterClientPacketHandler<PacketOwnershipChange>(OnClientPacketOwnershipChangeReceived);
            RegisterClientPacketHandler<PacketAuthorityChange>(OnClientPacketAuthorityChangeReceived);

            OnClientStart?.Invoke();
        }
        /// <summary>
        /// Stop the Client. Clears the handlers, invokes <c>OnClientStop</c> and calls <see cref="SharedStop"/>.
        /// </summary>
        public void ClientStop()
        {
            if (!IsClientRunning())
                return;

            _clientLocalClient.Stop();
            _clientLocalClient = null;

            _clientHandlers.Clear();

            SharedStop();

            OnClientStop?.Invoke();
        }
        /// <summary>
        /// Check if the Client is running.
        /// </summary>
        /// <returns>True if the Client is running.</returns>
        public bool IsClientRunning()
        {
            return _clientLocalClient != null && _clientLocalClient.IsRunning();
        }

        /// <summary>
        /// Register a Function to be called when the given <see cref="Packet"/> is received on the Client.
        /// Based off of https://stackoverflow.com/questions/30378593/register-event-handler-for-specific-subclass
        /// </summary>
        /// <typeparam name="T">The <see cref="Packet"/> to listen for.</typeparam>
        /// <param name="handler">The Function to call when the <see cref="Packet"/> of type <typeparamref name="T"/> is received.</param>
        public void RegisterClientPacketHandler<T>(Action<T> handler) where T : Packet
        {
            Action<Packet> wrapper = packet => handler(packet as T);
            if (_clientHandlers.ContainsKey(typeof(T)))
                _clientHandlers[typeof(T)] += wrapper;
            else
                _clientHandlers.Add(typeof(T), wrapper);
        }
        internal void ClientPacketReceived(Packet packet)
        {
            if (_clientHandlers.ContainsKey(packet.GetType()) && _clientHandlers[packet.GetType()] != null)
                _clientHandlers[packet.GetType()].Invoke(packet);
        }

        /// <summary>
        /// Send packets to the Server over TCP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToServerTCP(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            List<byte> packetBuffer = new List<byte>();
            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                // Packet Size + Packet
                packetBuffer.AddRange(BitConverter.GetBytes((uint)buffer.Length));
                packetBuffer.AddRange(buffer);
            }

            _ = _clientLocalClient.WriteTCP(packetBuffer.ToArray());
        }
        /// <summary>
        /// Send packets to the Server over UDP
        /// </summary>
        /// <param name="packets">The packets to send</param>
        public void SendToServerUDP(params Packet[] packets)
        {
            if (packets.Length <= 0)
                return;

            foreach (Packet packet in packets)
            {
                // Prepare Packet
                byte[] buffer = PacketFormatter.Serialise(packet);

                _ = _clientLocalClient.WriteUDP(buffer);
            }
        }

        // Static Functions
        /// <summary>
        /// Statically check if the Client is running.
        /// </summary>
        /// <returns>True if the Client is running.</returns>
        public static bool IsClient()
        {
            if (_instance == null)
                return false;

            return _instance.IsClientRunning();
        }
        /// <summary>
        /// Get the Client's network ID.
        /// Player IDs start at 1. 0 means no Client is running.
        /// </summary>
        /// <returns>0 if no Client is running, or the Player's network ID.</returns>
        public static uint GetClientID()
        {
            if (_instance == null)
            {
                Debug.LogError("Tried to GetClientID with no instance running");
                return 0;
            }
            if (!_instance.IsClientRunning())
            {
                Debug.LogError("Tried to GetClientID with no client running");
                return 0;
            }
            return _instance._clientID;
        }
        #endregion

        #region Client Handlers
        private void OnClientPacketPongReceived(PacketPong packet)
        {
            Debug.Log("Pong");
        }

        private void OnClientPacketServerInfoReceived(PacketServerInfo packet)
        {
            _clientID = packet.yourClientID;
        }

        private void OnClientPacketSpawnObjectReceived(PacketSpawnObject packet)
        {
            //Debug.Log($"Spawn: {packet.prefabID} {packet.ownerHasAuthority} {packet.positionX}");

            // Do not spawn duplicate if this client is host
            if (IsServerRunning() && _spawnedObjects.ContainsKey(packet.objectNetworkID))
            {
                NetworkObject temp = _spawnedObjects[packet.objectNetworkID];
                temp.HostSpawn(packet);
                ExecuteEvents.Execute<INetworkMessageTarget>(temp.gameObject, null, (x, y) => x.OnClientStart());
                return;
            }

            // Validate Packet
            if (packet.prefabID >= _spawnableObjects.Count)
            {
                Debug.LogError($"Server tried spawning {packet.prefabID}, but it's not in _spawnableObjects! (Max {_spawnableObjects.Count})");
                return;
            }
            if (_spawnedObjects.ContainsKey(packet.objectNetworkID))
            {
                Debug.LogError($"Server tried spawning {packet.objectNetworkID}, but it has already been spawned!");
                return;
            }

            NetworkObject networkObject = UnityEngine.Object.Instantiate(_spawnableObjects[(int)packet.prefabID]);
            networkObject.gameObject.transform.SetPositionAndRotation(packet.position, packet.rotation);
            networkObject.gameObject.transform.localScale = packet.localScale;

            _spawnedObjects[packet.objectNetworkID] = networkObject;
            networkObject.Spawn(packet.objectNetworkID, packet.ownerNetworkID, packet.ownerHasAuthority);
            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null, (x, y) => x.OnClientStart());
        }
        private void OnClientPacketDespawnObjectReceived(PacketDespawnObject packet)
        {
            // Do not spawn duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.objectNetworkID))
                return;

            UnityEngine.Object.Destroy(_spawnedObjects[packet.objectNetworkID].gameObject);
            _spawnedObjects.Remove(packet.objectNetworkID);
        }

        private void OnClientPacketOwnershipChangeReceived(PacketOwnershipChange packet)
        {
            // Validate Packet
            if (_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            uint oldValue = networkObject.GetOwnerID();
            networkObject.ChangeOwnership(packet.clientID);
            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null, (x, y) => x.OnOwnershipChange(oldValue, packet.clientID));
        }
        private void OnClientPacketAuthorityChangeReceived(PacketAuthorityChange packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            bool oldValue = networkObject.HasAuthority();
            networkObject.ChangeAuthority(packet.hasAuthority);
            ExecuteEvents.Execute<INetworkMessageTarget>(networkObject.gameObject, null, (x, y) => x.OnAuthorityChange(oldValue, packet.hasAuthority));
        }
        private void OnClientPacketNetworkBehaviourSyncReceived(PacketNetworkBehaviourSync packet)
        {
            // Do not duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            if (networkBehaviour.ShouldParseSyncPacket(packet))
                networkBehaviour.ParseSyncPacket(packet);
        }
        private void OnClientPacketClientRPCReceived(PacketClientRPC packet)
        {
            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.networkObjectID))
                return;
            NetworkObject networkObject = _spawnedObjects[packet.networkObjectID];
            if (!networkObject.HasNetworkBehaviour(packet.networkBehaviourID))
                return;
            NetworkBehaviour networkBehaviour = networkObject.GetNetworkBehaviour(packet.networkBehaviourID);

            networkBehaviour.OnPacketClientRPCReceive(packet);
        }

        private void OnClientPacketTargetedReceived(PacketTargeted packet)
        {
            // Do not duplicate if this client is host
            if (IsServerRunning())
                return;

            // Validate Packet
            if (!_spawnedObjects.ContainsKey(packet.target))
                return;

            ExecuteEvents.Execute<INetworkMessageTarget>(_spawnedObjects[packet.target].gameObject, null,
                (x, y) => x.OnPacketReceive(packet));
        }
        #endregion

        /// <summary>
        /// Set the spawnable Prefabs List. Cannot be done when the Server or Client is running.
        /// </summary>
        /// <param name="spawnableObjects">The List of <see cref="NetworkObject"/>s that can be spawned.</param>
        public void SetSpawnableObjects(List<NetworkObject> spawnableObjects)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _spawnableObjects = spawnableObjects;
        }
        /// <summary>
        /// Set the Player Prefab. Cannot be done when the Server or Client is running.
        /// </summary>
        /// <param name="playerPrefab">The <see cref="NetworkObject"/> prefab that is created when a Player joins (and is given ownership, but not necessarily authority, of).</param>
        public void SetPlayerPrefab(NetworkObject playerPrefab)
        {
            if (IsServerRunning() || IsClientRunning())
                return;
            _serverPlayerPrefab = playerPrefab;
        }

        /// <summary>
        /// Call when the Client loads into the Online Scene to indicate that it can start receiving <see cref="Packet"/>s from the Server.
        /// </summary>
        public void BeginProcessingPackets()
        {
            if (IsClientRunning())
                _clientLocalClient.SceneLoaded();
        }

        /// <summary>
        /// Get all of the spawned <see cref="NetworkObject"/>s in a Dictionary with their network ID.
        /// </summary>
        /// <returns></returns>
        public Dictionary<uint, NetworkObject> GetSpawnedObjects() { return _spawnedObjects; }

        public int GetTickRate() { return _tickRate; }
    }
}
