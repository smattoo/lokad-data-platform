﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Platform.Storage;
using Platform.StreamClients;

namespace Platform.TestClient.Commands
{
    public class BasicVerifyProcessor : ICommandProcessor
    {
        public string Key { get { return "BV"; } }
        public string Usage { get { return "BV [TIMEOUT(Sec) BatchSize BatchThreadCount FloodThreadCount]"; } }

        public bool Execute(CommandProcessorContext context, CancellationToken token, string[] args)
        {
            int timeOut = 30;
            int batchSize = 1000;
            int batchThreadCount = 4;
            int floodThreadCount = 4;

            if (args.Length > 0)
                int.TryParse(args[0], out timeOut);
            if (args.Length > 1)
                int.TryParse(args[1], out batchSize);
            if (args.Length > 2)
                int.TryParse(args[2], out batchThreadCount);
            if (args.Length > 3)
                int.TryParse(args[3], out floodThreadCount);

            return WriteFloodAndBatchTogether(context, timeOut, batchSize, batchThreadCount, floodThreadCount) |
                   ReadMessageWithNextOffset(context) |
                   WriteReadDifferentTypes(context) |
                   ViewClientReadWrite(context);

        }

        #region Write and Read Flood/Batch messages

        bool WriteFloodAndBatchTogether(CommandProcessorContext context, int timeOut, int batchSize, int batchThreadCount, int floodThreadCount)
        {
            string streamId = Guid.NewGuid().ToString();
            const string batchMsg = "BasicVerify-Batch-Test-Message";
            const string floodMsg = "BasicVerify-Flood-Test-Message";
            int batchCount = 0;
            int floodCount = 0;

            DateTime dt = DateTime.MaxValue;
            var errors = new ConcurrentStack<string>();
            var threads = new List<Task>();

            for (int t = 0; t < batchThreadCount; t++)
            {
                var task = Task.Factory.StartNew(() =>
                {
                    while (DateTime.Now < dt)
                    {
                        try
                        {
                            context.Client.Streams.WriteEventsInLargeBatch(streamId,
                            Enumerable.Range(0, batchSize).Select(
                                x =>
                                {
                                    var bytes = Encoding.UTF8.GetBytes(batchMsg);
                                    return new RecordForStaging(bytes);
                                }));
                            Interlocked.Add(ref batchCount, 1);
                        }
                        catch (Exception ex)
                        {
                            errors.Push(ex.Message);
                        }

                    }
                }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);
                threads.Add(task);
            }

            for (int t = 0; t < floodThreadCount; t++)
            {
                var task = Task.Factory.StartNew(() =>
                {
                    while (DateTime.Now < dt)
                    {
                        try
                        {
                            context.Client.Streams.WriteEvent(streamId, Encoding.UTF8.GetBytes(floodMsg));
                            Interlocked.Add(ref floodCount, 1);
                        }
                        catch (Exception ex)
                        {
                            errors.Push(ex.Message);
                        }
                    }
                }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);
                threads.Add(task);
            }
            dt = DateTime.Now.AddSeconds(timeOut);
            Task.WaitAll(threads.ToArray());



            Console.WriteLine("Add {0} flood messages", floodCount);
            Console.WriteLine("Add {0} batch", batchCount);

            return ReadAddMessages(context, streamId, batchMsg, floodMsg, errors, batchCount * batchSize, floodCount);
        }

        private static bool ReadAddMessages(CommandProcessorContext context, string streamId, string batchMsg, string floodMsg,
                                            ConcurrentStack<string> errors, int batchCount, int floodCount)
        {
            var records = context.Client.Streams.ReadAll().Where(x => x.Key == streamId);
            foreach (var record in records)
            {
                var msg = Encoding.UTF8.GetString(record.Data);
                if (msg.Equals(batchMsg))
                    batchCount--;
                else if (msg.Equals(floodMsg))
                    floodCount--;
                else
                    errors.Push("strange message: " + msg);
            }

            if (batchCount != 0)
                errors.Push("Unread " + batchCount + " batch messages");
            if (floodCount != 0)
                errors.Push("Unread " + floodCount + " flood messages");

            foreach (var err in errors.ToArray())
            {
                context.Log.Error(err);
            }

            return errors.Count == 0;
        }

        #endregion

        #region Read messages

        private bool ReadMessageWithNextOffset(CommandProcessorContext context)
        {
            var result = true;
            var records = context.Client.Streams.ReadAll(maxRecordCount: 20);
            RetrievedDataRecord prevRecord = default(RetrievedDataRecord);
            bool firstRecord = true;
            foreach (var record in records)
            {
                if (firstRecord)
                {
                    firstRecord = false;
                }
                else
                {
                    var prevNextRecord = context.Client.Streams.ReadAll(prevRecord.Next, 1).First();
                    var expectedBytes = record.Data.Except(prevNextRecord.Data).ToList();
                    if (record.Key != prevNextRecord.Key | expectedBytes.Count != 0)
                    {
                        context.Log.Error("Expected key: {0}, Received key: {1}", record.Key, prevNextRecord.Key);
                        context.Log.Error("Expected dat: {0}, Received key: {1}", record.Data.Length, prevNextRecord.Data.Length);
                        result = false;
                    }
                }

                prevRecord = record;
            }

            return result;
        }

        #endregion

        #region Write and read different types

        bool WriteReadDifferentTypes(CommandProcessorContext context)
        {
            string streamId = Guid.NewGuid().ToString();

            int intVal = 101;
            long longVal = 102;
            char charVal = 'A';
            string stringVal = "Hello server";
            DateTime dateVal = new DateTime(2012, 10, 25, 1, 2, 3);
            double doubleVal = 103.0;

            context.Client.Streams.WriteEvent(streamId, BitConverter.GetBytes(intVal));
            context.Client.Streams.WriteEvent(streamId, BitConverter.GetBytes(longVal));
            context.Client.Streams.WriteEvent(streamId, BitConverter.GetBytes(charVal));
            context.Client.Streams.WriteEvent(streamId, Encoding.UTF8.GetBytes(stringVal));
            context.Client.Streams.WriteEvent(streamId, BitConverter.GetBytes(dateVal.ToBinary()));
            context.Client.Streams.WriteEvent(streamId, BitConverter.GetBytes(doubleVal));

            var batchBody = new List<RecordForStaging>
                           {
                               new RecordForStaging(BitConverter.GetBytes(intVal)),
                               new RecordForStaging(BitConverter.GetBytes(longVal)),
                               new RecordForStaging(BitConverter.GetBytes(charVal)),
                               new RecordForStaging(Encoding.UTF8.GetBytes(stringVal)),
                               new RecordForStaging(BitConverter.GetBytes(dateVal.ToBinary())),
                               new RecordForStaging(BitConverter.GetBytes(doubleVal))
                           };

            context.Client.Streams.WriteEventsInLargeBatch(streamId, batchBody);

            var records = context.Client.Streams.ReadAll().Where(x => x.Key == streamId).ToList();
            bool result = true;

            for (int i = 0; i < 2; i++)
            {
                if (BitConverter.ToInt32(records[i * 6 + 0].Data, 0) != intVal)
                {
                    context.Log.Error("could not read the INT");
                    result = false;
                }
                if (BitConverter.ToInt64(records[i * 6 + 1].Data, 0) != longVal)
                {
                    context.Log.Error("could not read the LONG");
                    result = false;
                }
                if (BitConverter.ToChar(records[i * 6 + 2].Data, 0) != charVal)
                {
                    context.Log.Error("could not read the CHAR");
                    result = false;
                }
                if (Encoding.UTF8.GetString(records[i * 6 + 3].Data) != stringVal)
                {
                    context.Log.Error("could not read the STRING");
                    result = false;
                }
                if (DateTime.FromBinary(BitConverter.ToInt64(records[i * 6 + 4].Data, 0)) != dateVal)
                {
                    context.Log.Error("could not read the DATETIME");
                    result = false;
                }
                if (BitConverter.ToDouble(records[i * 6 + 5].Data, 0) != doubleVal)
                {
                    context.Log.Error("could not read the DOUBLE");
                    result = false;
                }
            }


            return result;
        }

        #endregion

        #region Write/Read view client

        bool ViewClientReadWrite(CommandProcessorContext context)
        {
            bool result = true;
            var views = context.Client.Views;
            views.CreateContainer();

            string streamId = Guid.NewGuid().ToString();
            var testData = Enumerable.Range(1, 100);

            context.Client.Streams.WriteEventsInLargeBatch(streamId,testData.Select(x=>new RecordForStaging(BitConverter.GetBytes(x))));

            var data = views.ReadAsJsonOrGetNew<ViewClientTest>(ViewClientTest.FileName);

            var records = context.Client.Streams.ReadAll(new StorageOffset(data.NextOffsetInBytes)).Where(x=>x.Key==streamId);

            foreach (var record in records)
            {
                data.Distribution.Add(BitConverter.ToInt32(record.Data,0));
                data.NextOffsetInBytes = record.Next.OffsetInBytes;
            }

            views.WriteAsJson(data, ViewClientTest.FileName);

            var writedData = views.ReadAsJsonOrGetNew<ViewClientTest>(ViewClientTest.FileName);

            if(data.NextOffsetInBytes!=writedData.NextOffsetInBytes)
            {
                context.Log.Error("Different Next.OffsetInBytes");
                result = false;
            }
            if(data.Distribution.Count!=writedData.Distribution.Count)
            {
                context.Log.Error("Different records count");
                result = false;
                return result;
            }

            for (int i = 0; i < data.Distribution.Count; i++)
            {
                if(data.Distribution[i]!=writedData.Distribution[i])
                {
                    context.Log.Error("Different record value");
                    result = false;
                }
            }

            return result;
        }

        private class ViewClientTest
        {
            public long NextOffsetInBytes { get; set; }
            public List<int> Distribution { get; private set; }
            public const string FileName = "ViewClientTest.dat";

            public ViewClientTest()
            {
                Distribution = new List<int>();
            }
        }

        #endregion
    }


}