using KitSymes.GTRP.Internal;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP
{
    public abstract class Packet
    {
        public virtual List<byte> Serialise()
        {
            List<byte> bytes = new List<byte>();
            foreach (System.Reflection.FieldInfo field in GetType().GetFields())
                bytes.AddRange(PacketFormatter.SerialiseObject(field.GetValue(this)));
            return bytes;
        }

        public virtual void Deserialise(byte[] bytes, int offset)
        {
            foreach (System.Reflection.FieldInfo field in GetType().GetFields())
            {
                field.SetValue(this, PacketFormatter.DeserialiseObject(field.FieldType, bytes, ref offset));
                //Debug.Log($"{field.Name}: {field.GetValue(this)}");
            }
        }
    }
}
