using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using cow_merger_service.Merger;
using cow_merger_service.Models;
using FASTER.core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace cow_merger_service
{
    public class SessionManager : IDisposable
    {
        private readonly string _destinationDirectory;
        private readonly ConcurrentDictionary<Guid, CowSession> _fileSessions = new();
        private readonly ILogger<SessionManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _originalImageDirectory;
        private readonly string _workingDirectory;
        private bool _disposed;

        public SessionManager(IConfiguration configuration, ILogger<SessionManager> logger,
            ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _workingDirectory = configuration["Settings:WorkingDirectory"];
            _originalImageDirectory = configuration["Settings:OriginalImageDirectory"];
            _destinationDirectory = configuration["Settings:DestinationDirectory"];
            _loggerFactory = loggerFactory;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        public Guid Create(string imageName, int version, int bitfieldSize)
        {
            CowSession session = new()
            {
                Id = Guid.NewGuid(),
                ImageName = imageName,
                ImageVersion = version,
                BitfieldSize = bitfieldSize
            };

            string path = Path.Combine(_workingDirectory, session.Id.ToString());


            Directory.CreateDirectory(path);

            string originalImagePath =
                Path.Combine(_originalImageDirectory, session.ImageName + ".r" + session.ImageVersion);
            if (!File.Exists(originalImagePath)) {
                _logger.Log(LogLevel.Warning,$"Image not found: {originalImagePath}");
                throw new ImageNotFound();
            }

            session.FileCopyTask = CopyImage(session);


            CreateKvStore(session);

            session.DataFileStream =
                File.Create(Path.Combine(path, Path.GetFileName(Path.Combine(_workingDirectory, "data"))));
            if (!_fileSessions.TryAdd(session.Id, session)) {
                _logger.Log(LogLevel.Error,$"Could not add session");
                throw new InvalidOperationException();
                }

            return session.Id;
        }

        private static void WriteSparse(CowSession session, in BlockMetadata metaData, Span<byte> spanData) {
            for (int i = 0; i < metaData.Bitfield.Length * 8; i++) {
                if (ByteArrayHelper.checkBit(metaData.Bitfield, i)) {
                    long diffOffset = metaData.Offset + i * 4096;

                    session.DataFileStream.Seek(diffOffset, SeekOrigin.Begin);
                    session.DataFileStream.Write(spanData.Slice(i*4096,4096));
                }
            }
            session.DataFileStream.SetLength(metaData.Offset+session.BitfieldSize*4096);
        }

        private bool UpdateBlock(CowSession session, int blockNumber, Span<byte> bitfield, Span<byte> spanData)
        {
            MyKey key = new()
            {
                Key = blockNumber
            };
            Status result = session.KvSession.Read(key, out BlockMetadata metadata);

            if (result.NotFound)
            {
                metadata.Number = blockNumber;
                metadata.Offset = session.DataFileStream.Length;
                session.TotalBlocks++;
                metadata.ModifyCount = 0;
                metadata.Bitfield = new byte[session.BitfieldSize];
            }

            ByteArrayHelper.Or(ref metadata.Bitfield, bitfield.ToArray());
            metadata.ModifyCount++;
            Status res = session.KvSession.Upsert(key, metadata);

            if (res.IsPending) session.KvSession.CompletePending(true);

            if (!res.IsCompletedSuccessfully)
            {
                Console.WriteLine("ERROR");
                return false;
            }
            if(spanData.Length < session.BitfieldSize*4096) {
                WriteSparse(session,in metadata,spanData);
            } else {
                session.DataFileStream.Seek(metadata.Offset, SeekOrigin.Begin);
                session.DataFileStream.Write(spanData);
            }
            session.LastUpDateTime = DateTime.Now;


            return true;
        }

        public bool Update(Guid guid, int blockNumber, Span<byte> spanData)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session))
            {
                session = LoadSessionFromFileSystem(guid);
                if (session == null) throw new KeyNotFoundException();
            }

            
            Span<byte> bitfield = spanData.Slice(0, session.BitfieldSize);
            Span<byte> data = spanData.Slice(session.BitfieldSize);
            lock (session.ObjLock)
            {
                return UpdateBlock(session, blockNumber, bitfield, data);
            }
        }

        public string StartMerge(Guid guid, long originalFileSize, long newFileSize)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session)) throw new KeyNotFoundException();

            session.NewFileSize = newFileSize;
            session.LastUpDateTime = DateTime.Now;
            session.OriginalFileSize = originalFileSize;
            lock (session.ObjLock)
            {
                if (session.FileCopyTask != null && session.FileCopyTask.Status == TaskStatus.Running)
                {
                    session.StartMerge = true;
                    return "Merge scheduled";
                }
            }

            if (session.FileCopyTask == null || session.FileCopyTask.IsCompletedSuccessfully)
            {
                Merge(session);
                return "Merge started";
            }

            throw new InvalidOperationException();
        }

        public SessionStatus Status(Guid guid)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session))
            {
                session = LoadSessionFromFileSystem(guid);
                if (session == null) throw new KeyNotFoundException();
            }

            return new SessionStatus
            {
                ImageName = session.ImageName,
                OriginalImageVersion = session.ImageVersion,
                NewImageVersion = -1, //TODO
                MergedClusters = session.MergedBlocks,
                TotalClusters = session.TotalBlocks
            };
        }

        private void CreateKvStore(CowSession session, bool recover = false)
        {
            string kvPath = Path.Combine(_workingDirectory, session.Id.ToString(), "meta/");
            session.Log = Devices.CreateLogDevice(kvPath + "hlog.log");

            session.Objlog = Devices.CreateLogDevice(kvPath + "hlog.obj.log");

            SerializerSettings<MyKey, BlockMetadata> serializerSettings =
                new SerializerSettings<MyKey, BlockMetadata>
                {
                    keySerializer = () => new KeySerializer(),
                    valueSerializer = () => new ValueSerializer()
                };

            session.Store = new FasterKV<MyKey, BlockMetadata>(
                1L << 20,
                new LogSettings { LogDevice = session.Log, ObjectLogDevice = session.Objlog },
                serializerSettings: serializerSettings,
                tryRecoverLatest: recover
            );

            session.KvSession = session.Store.NewSession(new SimpleFunctions<MyKey, BlockMetadata>());
        }

        private async Task CopyImage(CowSession session)
        {
            string originalImagePath =
                Path.Combine(_originalImageDirectory, session.ImageName + ".r" + session.ImageVersion);
            string path = Path.Combine(_workingDirectory, session.Id.ToString());
            await using (FileStream sourceStream = File.Open(originalImagePath, FileMode.Open))
            {
                //TODO increase ending
                await using (FileStream destinationStream = File.Create(Path.Combine(path,
                                 Path.GetFileName(Path.Combine(_originalImageDirectory, "img")))))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }
            }

            bool merge = false;
            lock (session.ObjLock)
            {
                if (session.StartMerge && session.State == SessionState.Copying)
                {
                    session.State = SessionState.Merging;
                    merge = true;
                }
                else
                {
                    session.State = SessionState.Active;
                }
            }

            if (merge)
            {
#pragma warning disable CS4014
                Merge(session);
#pragma warning restore CS4014
            }
        }

        private CowSession LoadSessionFromFileSystem(Guid guid)
        {
            string path = Path.Combine(_workingDirectory, guid.ToString());
            if (!Directory.Exists(path)) return null;

            CowSession session = JsonToFile.ReadFromJsonFile<CowSession>(Path.Combine(path, "session.json"));


            session.DataFileStream =
                File.OpenWrite(Path.Combine(path, Path.GetFileName(Path.Combine(_workingDirectory, "data"))));


            if (session.State != SessionState.Done)
            {
                CreateKvStore(session, true);
                session.LastUpDateTime = DateTime.Now;
                _fileSessions.TryAdd(session.Id, session);
            }

            return session;
        }

        private void SaveSessionToFile(CowSession session)
        {
            lock (session.ObjLock)
            {
                if (!_fileSessions.TryRemove(session.Id, out _)) return;

                string path = Path.Combine(_workingDirectory, session.Id.ToString());
                session.Store.TakeFullCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
                session.KvSession.Dispose();
                session.Store.Dispose();
                session.Log.Dispose();
                session.Objlog.Dispose();
                JsonToFile.WriteToJsonFile(Path.Combine(path, "session.json"), session);
            }
        }


        public List<BlockStatistics> GetTopModifiedBlocks(Guid guid, int amount)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session))
            {
                session = LoadSessionFromFileSystem(guid);
                if (session == null) throw new KeyNotFoundException();
            }

            if (session.State == SessionState.Done) return null;

            IFasterScanIterator<MyKey, BlockMetadata> iterator = session.KvSession.Iterate();
            List<BlockStatistics> blockStatisticsList = new();
            while (iterator.GetNext(out _))
            {
                BlockMetadata b = iterator.GetValue();
                BlockStatistics blockStatistics = new()
                {
                    ClusterNumber = b.Number,
                    Modifications = b.ModifyCount
                };
                blockStatisticsList.Add(blockStatistics);
            }

            blockStatisticsList.Sort((x, y) => y.Modifications.CompareTo(x.Modifications));
            return blockStatisticsList.GetRange(0,
                amount < blockStatisticsList.Count ? amount : blockStatisticsList.Count);
        }

        public static string ByteArrayToString(byte[] a)
        {
            return string.Join(" ", a.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')));
        }

        private Task Merge(CowSession session)
        {
            session.DataFileStream.Dispose();
            session.Merger = new
                Merger.Merger(Path.Combine(_workingDirectory, session.Id.ToString()),
                    _destinationDirectory,
                    session.BitfieldSize, session.ImageName, session.ImageVersion, _loggerFactory);
            session.State = SessionState.Merging;

            return Task.Factory.StartNew(() =>
            {
                if (session.Merger.Merge(session.KvSession, session.OriginalFileSize, session.NewFileSize))
                    session.State = SessionState.Done;
                else
                    session.State = SessionState.Failed;

                //TODO MAYBE DELETE?
                session.Store.TakeFullCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
                session.KvSession.Dispose();
                session.Store.Dispose();
                session.Log.Dispose();
                session.Objlog.Dispose();
                _logger.Log(LogLevel.Information,
                    $"Image:{session.ImageName}.{session.ImageVersion} {Environment.NewLine}" +
                    $"Guid:{session.Id} {Environment.NewLine}" +
                    "Successful Merged");
                SaveSessionToFile(session);
            });
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    foreach (KeyValuePair<Guid, CowSession> session in _fileSessions)
                        SaveSessionToFile(session.Value);

                _disposed = true;
            }
        }
    }

    public class ImageNotFound : Exception
    {
        public ImageNotFound()
        {
        }

        public ImageNotFound(string message)
            : base(message)
        {
        }

        public ImageNotFound(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}