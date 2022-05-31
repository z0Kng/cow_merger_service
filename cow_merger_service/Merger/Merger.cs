using System;
using System.IO;
using System.Linq;
using FASTER.core;


namespace cow_merger_service.Merger
{
    public class Merger
    {
        private readonly string _sourceDirectory;
        private readonly string _destination;
        private readonly long _bitFieldSize;
        private readonly string _originalImageName;
        private int _version;
        public long CurrentBlock;
        public long LastBlock;

   
        public Merger(string source, string destination, int bitFieldSize, string originalImageName, int version)
        {
            _sourceDirectory = source;
            _destination = destination;
            _bitFieldSize = bitFieldSize;
            _originalImageName = originalImageName;
            _version = version;
        }

        private bool checkBit(Span<byte> bitfield, int n)
        {
            return (((bitfield[n / 8])  >> (n % 8))  & 1 )> 0;
        }
        private string PrintB(bool[] a)
        {
            char[] buf = new char[a.Length];

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i])
                {
                    buf[i] = '1';
                }
                else
                {
                    buf[i] = '0';
                }
            }

            return new string(buf);
        }
        public static string ByteArrayToString(byte[] a)
        {

            return string.Join(" ", a.Select(x => Convert.ToString(x, 2).PadLeft(8, '0')));
        }

        
 

    
        public void Merge(ClientSession<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty, IFunctions<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty>> session, long newSize)
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
                    while (iterator.GetNext(out _))
                    {
                        BlockMetadata metaData = iterator.GetValue();
                        Console.WriteLine($"mergingBlock {metaData.Number}");
                        for (int i = 0; i < metaData.Bitfield.Length*8; i++) 
                        {
                            // TODO also check if more optimized seeking in diff is possible
                          
                            if (checkBit(metaData.Bitfield, i))
                            {

                                long diffOffset = metaData.Offset + i * 4096;
                                long fileOffset = metaData.Number * 4096 * _bitFieldSize + i * 4096;
                                diffStream.Seek(diffOffset, SeekOrigin.Begin);
                                fileStream.Seek(fileOffset, SeekOrigin.Begin);
                                diffStream.Read(buffer);
                                fileStream.Write(buffer);
                                

                            }
                        }
                    }
                    fileStream.SetLength(newSize);
                }
            }

            iterator.Dispose();

            _version++;

            while (File.Exists(Path.Combine(_destination, _originalImageName +".r"+ _version)))
            {
                _version++;
            }
            
            File.Move(sourceImage, Path.Combine(_destination, _originalImageName + ".r" + _version));
            File.Delete(dataFile);
            
        }


    }
}
