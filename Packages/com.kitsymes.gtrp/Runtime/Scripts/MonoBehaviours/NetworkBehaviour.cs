using UnityEngine;

namespace KitSymes.GTRP.MonoBehaviours
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkBehaviour : MonoBehaviour, INetworkMessageTarget
    {
        protected NetworkObject networkObject;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
        }

        public virtual void OnServerStart() { }
        public virtual void OnPacketReceive(Packet packet) { }

    }
}
