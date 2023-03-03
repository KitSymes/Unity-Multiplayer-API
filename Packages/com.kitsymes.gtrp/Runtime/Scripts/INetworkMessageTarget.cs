using UnityEngine.EventSystems;

namespace KitSymes.GTRP
{
    public interface INetworkMessageTarget : IEventSystemHandler
    {
        void OnServerStart();
        void OnPacketReceive(Packet packet);
    }
}
