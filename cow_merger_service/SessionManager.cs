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
        private readonly ConcurrentDictionary<Guid, CowSession> _fileSessions = new();

        private IConfiguration _configuration;
        private readonly string _workingDirectory;
        private readonly string _originalImageDirectory;
        private readonly string _destinationDirectory;
        private ILogger<SessionManager> _logger;
        private bool _disposed = false;
        public SessionManager(IConfiguration configuration, ILogger<SessionManager> logger) 
        {
            this._configuration = configuration;
            this._logger = logger;
            _workingDirectory = configuration["Settings:WorkingDirectory"];
            _originalImageDirectory = configuration["Settings:OriginalImageDirectory"];
            _destinationDirectory = configuration["Settings:DestinationDirectory"];
            _logger.Log(LogLevel.Information, $"workingDirectory: {_workingDirectory}");
            _logger.Log(LogLevel.Information, $"originalImageDirectory: {_originalImageDirectory}");
            _logger.Log(LogLevel.Information, $"destinationDirectory: {_destinationDirectory}");
        }

    
    

        public Guid Create(string imageName,int version, int bitfieldSize)
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

            string originalImagePath = Path.Combine(_originalImageDirectory, session.ImageName + ".r" + session.ImageVersion);
            if (!File.Exists(originalImagePath))
            {
                throw new ImageNotFound();
            }

            session.FileCopyTask = CopyImage(session);



            CreateKvStore(session);

            session.DataFileStream = File.Create(Path.Combine(path, Path.GetFileName(Path.Combine(_workingDirectory, "data"))));
            if (!_fileSessions.TryAdd(session.Id, session))
            {
                throw new InvalidOperationException();

            }

            return session.Id;
        }

        public bool Update(Guid guid, int blockNumber,  Span<byte> spanData)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session))
            {
                session = LoadSessionFromFileSystem(guid);
                if (session == null)
                {
                    throw new KeyNotFoundException();
                }
            }


            lock (session.ObjLock)
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
                }

                metadata.Bitfield = spanData.Slice(0, session.BitfieldSize / 8).ToArray();

                Status res = session.KvSession.Upsert(key, metadata);

                if (res.IsPending)
                {
                    session.KvSession.CompletePending(true);
                }

                if (!res.IsCompletedSuccessfully)
                {
                    Console.WriteLine("ERROR");
                    return false;
                }

                session.DataFileStream.Seek(metadata.Offset, SeekOrigin.Begin);
                session.DataFileStream.Write(spanData.Slice(session.BitfieldSize / 8));
                session.LastUpDateTime = DateTime.Now;
            }

            return true;

        }

        public string StartMerge(Guid guid, long fileSize)
        {
            if (!_fileSessions.TryGetValue(guid, out CowSession session))
            {
                throw new KeyNotFoundException();
            }

            session.FileSize = fileSize;
            session.LastUpDateTime = DateTime.Now;
            lock (session.ObjLock)
            {
                if (session.FileCopyTask!=null && session.FileCopyTask.Status == TaskStatus.Running)
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
                if (session == null)
                {
                    throw new KeyNotFoundException();
                }
            }
            
            return new SessionStatus()
            {
                ImageName = session.ImageName,
                OriginalImageVersion = session.ImageVersion,
                NewImageVersion = -1, //TODO
                MergedBlocks = session.MergedBlocks,
                TotalBlocks = session.TotalBlocks
            };
        }

        private void CreateKvStore(CowSession session, bool recover = false)
        {

            string kvPath = Path.Combine(_workingDirectory, session.Id.ToString(), "meta/");
            session.Log = Devices.CreateLogDevice(kvPath + "hlog.log");

            session.Objlog = Devices.CreateLogDevice(kvPath + "hlog.obj.log");

            var serializerSettings =
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

            string originalImagePath = Path.Combine(_originalImageDirectory, session.ImageName + ".r" + session.ImageVersion);
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
            if (!Directory.Exists(path))
            {
                return null;
            }

            CowSession session = JsonToFile.ReadFromJsonFile<CowSession>(Path.Combine(path,  "session.json"));



            session.DataFileStream = File.OpenWrite(Path.Combine(path, Path.GetFileName(Path.Combine(_workingDirectory, "data"))));


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
                if (!_fileSessions.TryRemove(session.Id, out _))
                {
                    return;
                }
                string path = Path.Combine(_workingDirectory, session.Id.ToString());
                session.Store.TakeFullCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
                session.KvSession.Dispose();
                session.Store.Dispose();
                session.Log.Dispose();
                session.Objlog.Dispose();
                JsonToFile.WriteToJsonFile(Path.Combine(path , "session.json"), session);
            }


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
                    session.BitfieldSize, session.ImageName, session.ImageVersion);
            session.State = SessionState.Merging;

            return Task.Factory.StartNew(() =>
            {
                session.Merger.Merge(session.KvSession, session.FileSize);
                session.State = SessionState.Done;
                //TODO MAYBE DELETE?
                session.Store.TakeFullCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
                session.KvSession.Dispose();
                session.Store.Dispose();
                session.Log.Dispose();
                session.Objlog.Dispose();
                _logger.Log(LogLevel.Information, $"Image:{session.ImageName}.{session.ImageVersion} {Environment.NewLine}" +
                                                  $"Guid:{session.Id} {Environment.NewLine}" +
                                                  "Successful Merged");
                SaveSessionToFile(session);
            });
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    foreach (KeyValuePair<Guid, CowSession> session in _fileSessions)
                    {
                        SaveSessionToFile(session.Value);
                    }
                }
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
