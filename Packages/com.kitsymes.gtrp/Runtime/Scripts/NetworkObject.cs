using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP
{
    public sealed class NetworkObject : MonoBehaviour
    {
        [SerializeField]
        private uint _prefabID;

        private uint _networkID;
        private uint _ownerNetworkID;

        private bool _spawned;

        private List<Packet> _tcpPackets;
        private List<Packet> _udpPackets;

        void Awake()
        {
            _tcpPackets = new List<Packet>();
            _udpPackets = new List<Packet>();
        }

        void Start()
        {
            if (!_spawned)
                gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_spawned && NetworkManager.IsServer())
                NetworkManager.Despawn(this);
        }

        public void Spawn(uint networkID, uint ownerNetworkID)
        {
            if (_spawned)
                return;
            _spawned = true;

            _networkID = networkID;
            _ownerNetworkID = ownerNetworkID;

            gameObject.SetActive(true);
        }

        public void AddTCPPacket(Packet packet) { _tcpPackets.Add(packet); }
        public List<Packet> GetTCPPackets() { return _tcpPackets; }
        public void AddUDPPacket(Packet packet) { _udpPackets.Add(packet); }
        public List<Packet> GetUDPPackets() { return _udpPackets; }

        public void ClearPackets()
        {
            _tcpPackets.Clear();
            _udpPackets.Clear();
        }

        public uint GetPrefabID() { return _prefabID; }
#if UNITY_EDITOR
        public void SetPrefabID(uint id) { _prefabID = id; }
#endif
        public uint GetNetworkID() { return _networkID; }
        public uint GetOwnerID() { return _ownerNetworkID; }
        public bool IsSpawned() { return _spawned; }
    }
}
