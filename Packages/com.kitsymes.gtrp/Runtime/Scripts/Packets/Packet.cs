using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KitSymes.GTRP
{
    [Serializable]
    public abstract class Packet
    {
        private uint _id;

        public Packet()
        {
            _id = PacketRegister.Instance.messageTypeToId[GetType()];
        }
    }
}
