﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FASTER.core;
using Microsoft.Extensions.Logging;

namespace cow_merger_service.Merger
{
    public class Merger
    {
        private readonly long _bitFieldSize;
        private readonly string _destination;
        private readonly ILogger<Merger> _logger;
        private readonly string _originalImageName;
        private readonly string _sourceDirectory;
        private int _version;


        public Merger(string source, string destination, int bitFieldSize, string originalImageName, int version,
            ILoggerFactory loggerFactory)
        {
            _sourceDirectory = source;
            _destination = destination;
            _bitFieldSize = bitFieldSize;
            _originalImageName = originalImageName;
            _version = version;
            _logger = loggerFactory.CreateLogger<Merger>();
        }

        public bool Merge(
            ClientSession<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty,
                IFunctions<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty>> session, long originalFileSize,
            long newSize)
        {
            string sourceImage = Path.Combine(_sourceDirectory, "img");
            string dataFile = Path.Combine(_sourceDirectory, "data");

            IFasterScanIterator<MyKey, BlockMetadata> iterator = session.Iterate();
            using (FileStream diffStream =
                   File.Open(dataFile, FileMode.Open))
            {
                using (FileStream fileStream =
                       File.Open(sourceImage, FileMode.Open))
                {
                    byte[] buffer = new byte[4096];
                    uint totalMerged = 0;
                    fileStream.SetLength(originalFileSize);
                    while (iterator.GetNext(out _))
                    {
                        BlockMetadata metaData = iterator.GetValue();
                        for (int i = 0; i < metaData.Bitfield.Length * 8; i++)
                            // TODO also check if more optimized seeking in diff is possible

                            if (ByteArrayHelper.checkBit(metaData.Bitfield, i))
                            {
                                long diffOffset = metaData.Offset + i * 4096;
                                long fileOffset = metaData.Number * 4096 * _bitFieldSize * 8 + i * 4096;
                                diffStream.Seek(diffOffset, SeekOrigin.Begin);
                                fileStream.Seek(fileOffset, SeekOrigin.Begin);
                                if (diffStream.Read(buffer) != 4096)
                                {
                                    _logger.Log(LogLevel.Error,
                                        "Reading less bytes than expected from data file. Cancel merge.");
                                    return false;
                                }

                                fileStream.Write(buffer);
                            }

                        totalMerged++;
                    }

                    _logger.Log(LogLevel.Debug, $"totalMerged: {totalMerged} Blocks");
                    fileStream.SetLength(newSize);
                }
            }

            iterator.Dispose();

            _version++;

            while (File.Exists(Path.Combine(_destination, _originalImageName + ".r" + _version))) _version++;

            File.Move(sourceImage, Path.Combine(_destination, _originalImageName + ".r" + _version));
            File.Delete(dataFile);
            return true;
        }
    }
}