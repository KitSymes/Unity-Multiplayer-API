using UnityEngine.EventSystems;

namespace KitSymes.GTRP
{
    public interface INetworkMessageTarget : IEventSystemHandler
    {
        void OnServerStart();
        void OnClientStart();
        void OnPacketReceive(Packet packet);
        void OnOwnershipChange(uint oldClient, uint newClient);
        void OnAuthorityChange(bool oldValue, bool newValue);
    }
}
