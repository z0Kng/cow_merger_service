using FASTER.core;

namespace cow_merger_service.Merger
{
    public class MyKey : IFasterEqualityComparer<MyKey>
    {
        public int Key;

        public long GetHashCode64(ref MyKey key)
        {
            return Utility.GetHashCode(key.Key);
        }

        public bool Equals(ref MyKey key1, ref MyKey key2)
        {
            return key1.Key == key2.Key;
        }
    }

    public class KeySerializer : BinaryObjectSerializer<MyKey>
    {
        public override void Serialize(ref MyKey key)
        {
            writer.Write(key.Key);
        }

        public override void Deserialize(out MyKey key)
        {
            key = new MyKey
            {
                Key = reader.ReadInt32()
            };
        }
    }

    public struct BlockMetadata
    {
        public int Number;
        public long Offset; //offset in the diff file
        public uint ModifyCount;
        public byte[] Bitfield;
    }

    public class ValueSerializer : BinaryObjectSerializer<BlockMetadata>
    {
        public override void Serialize(ref BlockMetadata value)
        {
            writer.Write(value.Number);
            writer.Write(value.Offset);
            writer.Write(value.Bitfield);
        }

        public override void Deserialize(out BlockMetadata value)
        {
            value = new BlockMetadata
            {
                Number = reader.ReadInt32(),
                Offset = reader.ReadInt64(),
                Bitfield = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))
            };
        }
    }


    public class MyContext
    {
    }
}