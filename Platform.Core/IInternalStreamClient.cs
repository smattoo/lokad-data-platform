﻿using System.Collections.Generic;
using System.Runtime.InteropServices;
using Platform.Storage;

namespace Platform
{
    /// <summary>
    /// Provides raw byte-level access to the storage and messaging of
    /// Data platform
    /// </summary>
    public interface IInternalStreamClient
    {
        /// <summary>
        /// Returns lazy enumeration over all events in a given record range. 
        /// </summary>
        IEnumerable<RetrievedDataRecord> ReadAll(StorageOffset startOffset = default (StorageOffset),
            int maxRecordCount = int.MaxValue);

        void WriteEvent(string streamName, byte[] data);
        void WriteEventsInLargeBatch(string streamName, IEnumerable<RecordForStaging> records);
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct StorageOffset
    {
        public readonly long OffsetInBytes;

        public static readonly StorageOffset Zero = new StorageOffset(0);

        public override string ToString()
        {
            return string.Format("Offset {0}b", OffsetInBytes);
        }

        public StorageOffset(long offsetInBytes)
        {
            Ensure.Nonnegative(offsetInBytes, "offsetInBytes");
            OffsetInBytes = offsetInBytes;
        }

        public static   bool operator >(StorageOffset x , StorageOffset y)
        {
            return x.OffsetInBytes > y.OffsetInBytes;
        }
        public static bool operator <(StorageOffset x , StorageOffset y)
        {
            return x.OffsetInBytes < y.OffsetInBytes;
        }
        public static bool operator >= (StorageOffset left, StorageOffset right)
        {
            return left.OffsetInBytes >= right.OffsetInBytes;
        }
        public static bool operator <=(StorageOffset left, StorageOffset right)
        {
            return left.OffsetInBytes <= right.OffsetInBytes;
        }


    }

    public struct RecordForStaging
    {
        public readonly byte[] Data;
        public RecordForStaging(byte[] data)
        {
            Data = data;
        }
    }


}