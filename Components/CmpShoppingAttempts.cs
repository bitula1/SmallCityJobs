using Colossal.Serialization.Entities;
using Unity.Entities;

namespace BitulaMod.Components {
    public struct CmpShoppingAttempts : IComponentData, ISerializable {
        private const int SerializationVersion = 1;

        public uint m_Day;
        public byte m_TryCount;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(SerializationVersion);
            writer.Write(m_Day);
            writer.Write(m_TryCount);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_Day);
            reader.Read(out m_TryCount);
        }
    }
}