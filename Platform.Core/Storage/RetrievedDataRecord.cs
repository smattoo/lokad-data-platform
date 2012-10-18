using System.Collections.Generic;

namespace Platform.Storage
{
    public struct RetrievedDataRecord
    {
        public bool IsEmpty { get { return Data == null; } }

        public static readonly ICollection<RetrievedDataRecord> EmptyList = new RetrievedDataRecord[0];

        public readonly string Key;
        public readonly byte[] Data;
        public readonly StorageOffset NextOffset;

        public RetrievedDataRecord(string key, byte[] data,StorageOffset nextOffset) 
        {
            Key = key;
            Data = data;
            NextOffset = nextOffset;
        }
    }
}