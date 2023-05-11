using KitSymes.GTRP.Internal;
using System.Collections.Generic;
using System.Reflection;

namespace KitSymes.GTRP
{
    public abstract class Packet
    {
        public virtual List<byte> Serialise()
        {
            // A list of Bytes representing packed Booleans, indicating if the relevant field is set
            List<byte> header = new List<byte>();
            // A list of all the serialised field data
            List<byte> bytes = new List<byte>();

            //UnityEngine.Debug.Log("-========== Serialising ==========-");

            int fieldCount = 0;
            MethodInfo method = typeof(ByteConverter).GetMethod(nameof(ByteConverter.SerialiseArgument));
            foreach (System.Reflection.FieldInfo field in GetType().GetFields())
            {
                //UnityEngine.Debug.Log($"{field.Name} {bytes.Count}");

                // If the field count is divisible by 8, we need a new byte
                if (fieldCount % 8 == 0)
                    header.Add(0);

                object value = field.GetValue(this);

                if (value != null)
                {
                    // Set the field header bit
                    header[fieldCount / 8] |= (byte)(1 << fieldCount % 8);
                    MethodInfo generic = method.MakeGenericMethod(field.FieldType);
                    bytes.AddRange((byte[])generic.Invoke(null, new object[] { value }));
                }

                fieldCount++;
            }

            List<byte> packet = new List<byte>();
            packet.AddRange(ByteConverter.SerialiseArgument<int>(header.Count));
            packet.AddRange(header);
            packet.AddRange(bytes);
            return packet;
        }

        public virtual void Deserialise(byte[] bytes, int offset)
        {
            int headerSize = (int)ByteConverter.DeserialiseArgument<int>(bytes, ref offset);
            List<byte> header = new List<byte>();
            for (int i = 0; i < headerSize; i++)
                header.Add((byte)ByteConverter.DeserialiseArgument<byte>(bytes, ref offset));

            //UnityEngine.Debug.Log("-========== Deserialising ==========-");

            int fieldCount = 0;
            MethodInfo method = typeof(ByteConverter).GetMethod(nameof(ByteConverter.DeserialiseArgument));
            foreach (System.Reflection.FieldInfo field in GetType().GetFields())
            {
                //UnityEngine.Debug.Log($"{field.Name} {offset}");

                object value = null;
                // Check to see if the field is set
                if (((header[fieldCount / 8] >> fieldCount % 8) & 1) != 0)
                {
                    MethodInfo generic = method.MakeGenericMethod(field.FieldType);
                    object[] parameters = new object[] { bytes, offset };
                    value = generic.Invoke(null, parameters);
                    offset = (int)parameters[1];
                }
                field.SetValue(this, value);
                //Debug.Log($"{field.Name}: {field.GetValue(this)}");

                fieldCount++;
            }
        }
    }
}
