using System;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using cow_merger_service.Merger;
using FASTER.core;
using Newtonsoft.Json;


namespace cow_merger_service
{
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SessionState
    {
        Copying,
        Active,
        Merging,
        Done
    }

    public class CowSession
    {
        public Guid Id { get; set; }

        public SessionState State { get; set; }
        
        public long FileSize { get; set; }
        public bool StartMerge { get; set; } =  false;


        
        public int ProtocolVersion { get; set; } = 1;
        public string ImageName { get; set; }
        public int ImageVersion { get; set; }
        public int BitfieldSize { get; set; }
        public int MergedBlocks { get; set; }
        public int TotalBlocks { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public object ObjLock { get; set; } = new();

        [Newtonsoft.Json.JsonIgnore]
        public Task FileCopyTask { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public FileStream DataFileStream { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public DateTime LastUpDateTime { get; set; } = DateTime.Now;

        [Newtonsoft.Json.JsonIgnore]
        public FasterKV<MyKey, BlockMetadata> Store { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public ClientSession<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty, IFunctions<MyKey, BlockMetadata, BlockMetadata, BlockMetadata, Empty>> KvSession { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public IDevice Objlog { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public IDevice Log { get; set; }


        [Newtonsoft.Json.JsonIgnore]
        public Merger.Merger Merger;

      

        ~CowSession()
        {

            DataFileStream.Dispose();
            
            Store.TakeFullCheckpointAsync(CheckpointType.FoldOver).GetAwaiter().GetResult();
            KvSession.Dispose();
            Store.Dispose();
            Log.Dispose();
            Objlog.Dispose();
        }
    }
    public class JsonToFile
    {

        /// <summary>
        /// Writes the given object instance to a Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [JsonIgnore] attribute.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToJsonFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                string contentsToWriteToFile = JsonConvert.SerializeObject(objectToWrite);
                writer = new StreamWriter(filePath, append);
                writer.Write(contentsToWriteToFile);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an Json file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the Json file.</returns>
        public static T ReadFromJsonFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                reader = new StreamReader(filePath);
                string fileContents = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<T>(fileContents);
            }
            finally
            {
                reader?.Close();
            }
        }


    }
}
