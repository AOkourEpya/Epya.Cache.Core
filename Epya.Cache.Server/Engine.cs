using Epya.Cache.Common;
using Epya.Cache.Common.Tcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Epya.Cache.Server
{
    public class Engine
    {
        public static  List<EntityMetadataDto> ContextMetadata=new List<EntityMetadataDto>();

        public int success_writes = 0; // Managed with Interlocked
        public int fail_writes = 0; // Managed with Interlocked
        public bool isRunning = false;

        private readonly CacheConfigDto configs;
        private readonly string serverAddress;
        private readonly int readPort;
        private readonly int writePort;

        public List<cacheReplica> replicas = null;
        private readonly int replicaSize;
        private int _replicaIndex = 0;
        private readonly object _replicaIndexlock = new object();

        TcpManager tcpReadManager = null;
        TcpManager tcpWriteManager = null;

        public Engine(CacheConfigDto configs, int replicaSize=1)
        {
            ArgumentNullException.ThrowIfNull(configs);

            this.configs = configs;
            serverAddress = configs.ServerAddress;
            readPort = configs.ReadPort;
            writePort = configs.OthersPort;

            this.replicaSize = replicaSize;
            // outerLogger = new FileLogger(@"C:\AppLogs\CacheServer\log.txt", targetLevel);
            ContextMetadata = _LoadContextMetadataFromFile();
        }

        public void StartServer()
        {
            try
            {
                replicas = new List<cacheReplica>();
                for (int i=0;i<replicaSize ;i++ ) {
                    replicas.Add(new cacheReplica());
                }

                tcpReadManager = new TcpManager(readPort);
                tcpReadManager.OnMessageReceivedAsync = _RecieveReadMessageAsync;
                tcpReadManager.StartServerAsync();

                tcpWriteManager = new TcpManager(writePort);
                tcpWriteManager.OnMessageReceivedAsync = _RecieveWriteMessageAsync;
                tcpWriteManager.StartServerAsync();

                isRunning = true;
            }
            catch
            {
                StopServer();
                throw;
            }
           
        }

        public void StopServer()
        {
            isRunning = false;

            if (tcpReadManager != null) tcpReadManager.StopServer();
            if (tcpWriteManager != null) tcpWriteManager.StopServer();

            if (replicas != null)
            {
                foreach (cacheReplica replica in replicas)
                {
                    //clear cache
                    replica.Clear();
                }
                replicas = null;
            }

            success_writes = 0;
            fail_writes = 0;
        }

        private async Task<string> _RecieveReadMessageAsync(string message)
        {
            int replicaIndex = _GetAndIncrement();

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                CacheResponse res = null;
                if (!CommonOperations.IsValidJson(message))
                {
                    res = new CacheResponse { ResponseCode = "500", ErrorMessage = "the recived message is not valid json" };
                    Interlocked.Increment(ref replicas[replicaIndex].failReads);
                }
                else
                {
                    CacheRequest req = JsonConvert.DeserializeObject<CacheRequest>(message);
                    res = _ProcessReadRequest(req, replicaIndex);
                    long cacheSearchTime = stopwatch.ElapsedMilliseconds;


                    if (res.ResponseCode == "200")
                        Interlocked.Increment(ref replicas[replicaIndex].successReads);
                    else
                        Interlocked.Increment(ref replicas[replicaIndex].failReads);

                }
                stopwatch.Stop();

                return JsonConvert.SerializeObject(res);
            }
            catch {
                //write log
                Interlocked.Increment(ref replicas[replicaIndex].failReads);
                throw;
            }

        }     
        private async Task<string> _RecieveWriteMessageAsync(string message)
        {

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();

                CacheResponse res = null;
                if (!CommonOperations.IsValidJson(message))
                {
                    res = new CacheResponse { ResponseCode = "500", ErrorMessage = "the recived message is not valid json" };
                    Interlocked.Increment(ref fail_writes);
                }
                else
                {             
                    CacheRequest req = JsonConvert.DeserializeObject<CacheRequest>(message);
                    switch (req.Action)
                    {
                        case Common.Action.Write:
                            res = await _HandleWriteAsync(req);
                            break;
                        case Common.Action.CheckImpact:
                            res = await _HandleCheckImpactAsync(req);
                            break;

                        case Common.Action.ExportMetadata:
                            res = await _HandleImportMetadataAsync(req);
                            break;
                    }
                }             

                stopwatch.Stop();

                return JsonConvert.SerializeObject(res);
            }
            catch
            {
                //write log                
                throw;
            }

        }
        private CacheResponse _ProcessReadRequest(CacheRequest request, int replicaIndex)
        {
            CacheResponse response = new CacheResponse();
            string guid;
            dynamic cachedResponse;
            Dictionary<string, string> cachedHeaders;
            if (replicas[replicaIndex].cacheMap.TryGetValue(request.CacheKey, out guid) &&
                replicas[replicaIndex].cacheData.TryGetValue(guid, out cachedResponse) &&
                replicas[replicaIndex].cacheHeaders.TryGetValue(guid, out cachedHeaders))
            {
                response.ResponseCode = "200";
                response.Headers = cachedHeaders;
                response.Body = cachedResponse;
            }
            else
            {
                response.ResponseCode = "404";
            }
            return response;

        }
        private async Task<CacheResponse> _HandleWriteAsync(CacheRequest request)
        {
            CacheResponse response = new CacheResponse();
            try
            {
                List<string> includes = null;
                var queryString = _ExtractQueryString(request.FullUrl);
                if(queryString != null && queryString.Any(i=>i.Key.ToLower()=="include" || i.Key.ToLower() == "includes"))
                {
                    includes = queryString["include"]?.Split(',').Select(i => i.Trim()).ToList() ?? 
                        queryString["includes"]?.Split(',').Select(i => i.Trim()).ToList();
                }

                (List<string> directDepandancy, List<string> indirectDepandancy) dependancesResult = _ScanJsonForDepandancy(request.Body,includes);
                

                //List<string> dependents = ScanJsonForProperties(request.Body);
                string guid = Guid.NewGuid().ToString();
                
                foreach(cacheReplica replica in replicas)
                {
                    if (replica.cacheMap.TryAdd(request.CacheKey, guid))
                    {
                        replica.cacheMeta.TryAdd(guid, new CacheMeta { Timestamp = DateTime.Now, CacheKey = request.CacheKey});
                        replica.cacheData.TryAdd(guid, request.Body);
                        replica.cacheHeaders.TryAdd(guid, request.Headers);
                        if (dependancesResult.directDepandancy != null && dependancesResult.directDepandancy.Count > 0)
                        {
                            foreach (string dependent in dependancesResult.directDepandancy)
                            {
                                replica.directCacheDependances.AddOrUpdate(dependent, guid + ",", (key, oldValue) => { return (!oldValue.Contains(guid + ",")) ? (oldValue + guid + ",") : oldValue; });
                            }
                        }

                        if (dependancesResult.indirectDepandancy != null && dependancesResult.indirectDepandancy.Count > 0)
                        {
                            foreach (string dependent in dependancesResult.indirectDepandancy)
                            {
                                replica.indirectCacheDependances.AddOrUpdate(dependent, guid + ",", (key, oldValue) => { return (!oldValue.Contains(guid + ",")) ? (oldValue + guid + ",") : oldValue; });
                            }
                        }
                        response.ResponseCode = "200";
                        Interlocked.Increment(ref success_writes);
                        Interlocked.Increment(ref replica.successWrites);

                    }
                    else
                    {
                        response.ResponseCode = "500";
                        response.ErrorMessage = "The cache key already exists";
                        Interlocked.Increment(ref fail_writes);
                        Interlocked.Increment(ref replica.failWrites);

                        break;
                    }

                }            

                return response;
               
            }
            catch (Exception ex)
            {                
               
                Interlocked.Increment(ref fail_writes);
                response.ResponseCode = "500";
                response.ErrorMessage = ex.Message;
                response.ErrorStackTrace = ex.StackTrace;

                return response;

            }
        }
        private async Task<CacheResponse> _HandleImportMetadataAsync(CacheRequest request)
        {
            try
            {
                Engine.ContextMetadata = JsonConvert.DeserializeObject<List<EntityMetadataDto>>(request.Body);
                _SaveContextMetadataJsonToFile(request.Body);

                CacheResponse response = new CacheResponse() { ResponseCode = "200" };

                return response;
            }
            catch (Exception ex)
            {

                CacheResponse response = new CacheResponse { ResponseCode = "500", ErrorMessage = ex.Message, ErrorStackTrace = ex.StackTrace };
                return response;
            }
        }
        private async Task<CacheResponse> _HandleCheckImpactAsync(CacheRequest request)
        {
            try
            {
                List<EntityActionMetadata> entityActionMetadata = JsonConvert.DeserializeObject<List<EntityActionMetadata>>(request.Body);
                if (entityActionMetadata != null && entityActionMetadata.Count > 0)
                {
                    //entityActionMetadata.ForEach(e => e.Type = _DecodeTypeName(e.Type));

                    List<EntityActionMetadata> directActionEntities = entityActionMetadata.Where(e => e.Operation == EntityOperation.Delete || e.Operation == EntityOperation.Update).ToList();
                    List<EntityActionMetadata> indirectActionEntities = entityActionMetadata.Where(e => e.Operation == EntityOperation.Add || e.Operation == EntityOperation.Update).ToList();

                    if (directActionEntities != null && directActionEntities.Count > 0)
                        _HandleDirectImpact(directActionEntities);

                    if (indirectActionEntities != null && indirectActionEntities.Count > 0)
                        _HandleInDirectImpact(indirectActionEntities);


                    if (entityActionMetadata.Any(e => e.Operation == EntityOperation.None))
                    {
                        //log warning about that we recieve none operations on some entities 
                    }
                }

                CacheResponse response = new CacheResponse() { ResponseCode = "200" };
                return response;

            }
            catch (Exception ex)
            {
                CacheResponse response = new CacheResponse() { ResponseCode = "500", ErrorMessage = ex.Message, ErrorStackTrace = ex.StackTrace };
                return response;
            }
        }
        private void _HandleDirectImpact(List<EntityActionMetadata> entites)
        {

            //directCacheDependances is a collection
            //of key value pairs where the key is the entity type and id concatenated with a pipe (|) and the value is a comma separated list of cache guids that depend on that entity

            if (entites == null || entites.Count == 0)
                return;

            ConcurrentBag<string> deletedCacheGuids = new ConcurrentBag<string>();

            foreach (var entity in entites)
            {
                foreach (cacheReplica replica in replicas)
                {
                    if (replica.directCacheDependances.TryGetValue(entity.Type + "|" + entity.Id, out string guids))
                    {
                        foreach (string g in guids.TrimEnd(',').Split(','))
                        {
                            replica.cacheData.TryRemove(g, out dynamic val1);
                            replica.cacheHeaders.TryRemove(g, out Dictionary<string, string> val2);
                            if (replica.cacheMeta.TryRemove(g, out CacheMeta val3))
                                replica.cacheMap.TryRemove(val3.CacheKey, out string val4);

                            if (deletedCacheGuids.Where((guid) => guid == g).SingleOrDefault() == null)
                                deletedCacheGuids.Add(g);
                        }
                        replica.directCacheDependances.TryRemove(entity.Type + "|" + entity.Id, out string val5);
                    }
                }
            }

            if (deletedCacheGuids.Count > 0)
                _ = Task.Run(() => _CleanCachDepandancy(deletedCacheGuids)).ConfigureAwait(false);
        }
        private void _HandleInDirectImpact(List<EntityActionMetadata> entites)
        {

            //indirectCacheDependances is a collection
            //of key value pairs where the key is the entity type,id,related entity (collection) and forighn key concatenated with a pipe (|) and the value is a comma separated list of cache guids that depend on that entity

            if (entites == null || entites.Count == 0)
                return;

            if (ContextMetadata == null || ContextMetadata.Count == 0)
            {
                //log warning about that we don't have context metadata
                //log warning about that we can't handle indirect impact without context metadata
                return;
            }

            ConcurrentBag<string> deletedCacheGuids = new ConcurrentBag<string>();

            foreach (var entity in entites)
            {

                if (entity.Fks == null || entity.Fks.Count == 0)
                    continue;

                var entityMetadata = ContextMetadata
                       .Where(m => m.EntityName.ToLower() == entity.Type.ToLower()).FirstOrDefault();

                foreach (var fk in entity.Fks)
                {
                    if (fk.Value == null)
                        continue;
                    var contextEntityfk = entityMetadata.ForeignKeys.Where(efk => string.Equals(efk.ForeignKeyProperty, fk.Key, StringComparison.OrdinalIgnoreCase) && efk.RelationType == "Single").FirstOrDefault();
                    if (contextEntityfk == null || contextEntityfk.InverseCollectionNavigation == null)
                        continue;

                    string indirectKey = contextEntityfk.RelatedEntityTypeFullName.ToLower() + "|" + fk.Value + "|" + contextEntityfk.InverseCollectionNavigation.ToLower() + "|" + entity.Type.ToLower() + "|" + fk.Key.ToLower();

                    foreach (cacheReplica replica in replicas)
                    {
                        if (replica.indirectCacheDependances.TryGetValue(indirectKey, out string guids))
                        {
                            foreach (string g in guids.TrimEnd(',').Split(','))
                            {
                                replica.cacheData.TryRemove(g, out dynamic val1);
                                replica.cacheHeaders.TryRemove(g, out Dictionary<string, string> val2);
                                if (replica.cacheMeta.TryRemove(g, out CacheMeta val3))
                                    replica.cacheMap.TryRemove(val3.CacheKey, out string val4);

                                if (deletedCacheGuids.Where((guid) => guid == g).SingleOrDefault() == null)
                                    deletedCacheGuids.Add(g);
                            }
                            replica.indirectCacheDependances.TryRemove(indirectKey, out string val5);
                        }
                    }
                }
            }


            if (deletedCacheGuids.Count > 0)
                _ = Task.Run(() => _CleanCachDepandancy(deletedCacheGuids)).ConfigureAwait(false);
        }
        private void _CleanCachDepandancy(ConcurrentBag<string> guids) {

            foreach(cacheReplica replica in replicas)
            {
                // Create a partitioner for optimized parallel processing
                var partitioner = Partitioner.Create(replica.directCacheDependances.Keys, EnumerablePartitionerOptions.NoBuffering);

                Parallel.ForEach(partitioner, key =>
                {
                    if (replica.directCacheDependances.TryGetValue(key, out string currentValue))
                    {
                        var updatedValues = currentValue
                            .Split(',')
                            .Where(v => !guids.Contains(v))
                            .ToArray();

                        var newVal = string.Join(",", updatedValues);
                        if (!string.IsNullOrEmpty(newVal))
                            replica.directCacheDependances[key] = string.Join(",", updatedValues);
                        else
                            replica.directCacheDependances.TryRemove(key, out string v);
                    }
                });

                partitioner = Partitioner.Create(replica.indirectCacheDependances.Keys, EnumerablePartitionerOptions.NoBuffering);

                Parallel.ForEach(partitioner, key =>
                {
                    if (replica.indirectCacheDependances.TryGetValue(key, out string currentValue))
                    {
                        var updatedValues = currentValue
                            .Split(',')
                            .Where(v => !guids.Contains(v))
                            .ToArray();

                        var newVal = string.Join(",", updatedValues);
                        if (!string.IsNullOrEmpty(newVal))
                            replica.indirectCacheDependances[key] = string.Join(",", updatedValues);
                        else
                            replica.indirectCacheDependances.TryRemove(key, out string v);
                    }
                });

            }
            

        }
        private int _GetAndIncrement()
        {
            lock (_replicaIndexlock)
            {
                int current = _replicaIndex;
                _replicaIndex += 1;

                if (_replicaIndex >= replicaSize) // Reset to 0 when exceeding ReplicaSize
                {
                    _replicaIndex = 0;
                }

                return current;
            }
        }   
        private string _DecodeTypeName(string encoded)
        {
            string[] parts = encoded.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder typename = new StringBuilder();

            foreach (string part in parts)
            {
                if (part == "0")
                {
                    typename.Append('.');
                }
                else if (int.TryParse(part, out int val))
                {
                    if (val >= 1 && val <= 26)
                    {
                        char c = (char)('a' + val - 1);
                        typename.Append(c);
                    }
                    else
                    {
                        throw new InvalidOperationException("Invalid encoded character value: " + val);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Invalid encoded part: " + part);
                }
            }

            return typename.ToString();
        }
        private Dictionary<string, string> _ExtractQueryString(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.IndexOf('?')<0)
                return null;

            Dictionary<string, string> result = null;
            url = url.Substring(url.IndexOf('?'));
            var response= HttpUtility.ParseQueryString(url);
            if(response != null)
                result= response.AllKeys
                       .Where(k => k != null)
                       .ToDictionary(k => k, k => response[k]);

            return result;
        }
        private (List<string> directDepandancy, List<string> indirectDepandancy) _ScanJsonForDepandancy(string json, List<string> includes)
        {
            List<string> directDepandancy = new List<string>();
            List<string> indirectDepandancy = new List<string>();
            JToken root = JToken.Parse(json);
            List<EntityMetadataDto> metadata = ContextMetadata;            

            void Traverse(JToken token, string path)
            {
                if (token.Type == JTokenType.Object)
                {
                    JObject obj = (JObject)token;
                    JProperty tProperty = obj.Properties()
                        .FirstOrDefault(p => string.Equals(p.Name, "t", StringComparison.OrdinalIgnoreCase));
                    JProperty idProperty = obj.Properties()
                        .FirstOrDefault(p => string.Equals(p.Name, "id", StringComparison.OrdinalIgnoreCase));

                    string currentType = _DecodeTypeName(tProperty.Value.ToString()).ToLower();
                    string currentId = idProperty.Value.ToString();

                    if (tProperty != null && idProperty != null)
                    {
                        directDepandancy.Add(currentType + "|" + currentId);
                    }

                    foreach (var prop in token.Children<JProperty>())
                    {
                        var value = prop.Value;
                        if (value.Type == JTokenType.Object)
                        {
                            Traverse(value, $"{path}.{prop.Name}");
                        }
                        if (value.Type == JTokenType.Array)
                        {

                            string fkProperty = "";
                            string relatedEntity = "";

                            //find information about the collection and it's FK property 
                            var entityMetadata = metadata
                                .Where(m => m.EntityName.ToLower() == currentType.ToLower() && m.ForeignKeys.Any((f) => f.RelationType == "Collection" && f.NavigationProperty.ToLower() == prop.Name.ToLower())).FirstOrDefault();
                            if (entityMetadata != null)
                            {
                                fkProperty = entityMetadata.ForeignKeys
                                    .FirstOrDefault(f => f.RelationType == "Collection" && f.NavigationProperty == prop.Name)?.ForeignKeyProperty.ToLower();
                                relatedEntity = entityMetadata.ForeignKeys
                                    .FirstOrDefault(f => f.RelationType == "Collection" && f.NavigationProperty == prop.Name)?.RelatedEntityTypeFullName.ToLower();

                                var collectionName = prop.Name.ToLower();

                                var array = prop.Value as JArray;
                                if (array == null || array.Count == 0)
                                {
                                    // if the array is null or empty, check if the path is included in the includes
                                    var currentPath = $"{path}.{prop.Name}".TrimStart('.');
                                    var isArrayIncluded = includes?.Any(i => i == currentPath);
                                    if (isArrayIncluded != null && isArrayIncluded.Value)
                                        indirectDepandancy.Add($"{currentType}|{currentId}|{collectionName}|{relatedEntity}|{fkProperty}");
                                }
                                else
                                {
                                    //if the array not null or empty we are checking t property exitance inside the objects of the array
                                    var firstChildWithT = array?
                                   .OfType<JObject>()
                                   .FirstOrDefault(child => child.Properties()
                                       .Any(p => string.Equals(p.Name, "t", StringComparison.OrdinalIgnoreCase)));

                                    if (firstChildWithT != null)
                                        indirectDepandancy.Add($"{currentType}|{currentId}|{collectionName}|{relatedEntity}|{fkProperty}");
                                }
                            }

                            Traverse(value, $"{path}.{prop.Name}");
                        }
                    }
                }
                else if (token.Type == JTokenType.Array)
                {
                    foreach (var item in token.Children())
                    {
                        if (item.Type == JTokenType.Object || item.Type == JTokenType.Array)
                        {
                            Traverse(item, path); // No array keyword or index
                        }
                    }
                }
            }

            Traverse(root, "");
            return (directDepandancy, indirectDepandancy);
        }
        private void _SaveContextMetadataJsonToFile(string json)
        {
            string filePath = _GetMetadataFilePath();           
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
        private List<EntityMetadataDto> _LoadContextMetadataFromFile()
        {
            string filePath = _GetMetadataFilePath();
            if (!File.Exists(filePath))
                return new List<EntityMetadataDto>();

            string json = File.ReadAllText(filePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<EntityMetadataDto>>(json);
        }
        private string _GetMetadataFilePath()
        {
            string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string folderPath = Path.Combine(baseDir, "ContextMetadata");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            return Path.Combine(folderPath, "context_metadata.json");
        }

    }

    public class cacheReplica
    {
        public ConcurrentDictionary<string, string> cacheMap = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<string, dynamic> cacheData = new ConcurrentDictionary<string, dynamic>();
        public ConcurrentDictionary<string, Dictionary<string, string>> cacheHeaders = new ConcurrentDictionary<string, Dictionary<string, string>>();
        public ConcurrentDictionary<string, CacheMeta> cacheMeta = new ConcurrentDictionary<string, CacheMeta>();
        public ConcurrentDictionary<string, string> directCacheDependances = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<string, string> indirectCacheDependances = new ConcurrentDictionary<string, string>();

        public string Username="";
        public int successReads = 0; // Managed with Interlocked
        public int failReads = 0; // Managed with Interlocked
        public int successWrites = 0; // Managed with Interlocked
        public int failWrites = 0; // Managed with Interlocked

        public void Clear()
        {
            cacheMap.Clear();
            cacheData.Clear();
            cacheHeaders.Clear();
            directCacheDependances.Clear();
            indirectCacheDependances.Clear();

            cacheMeta.Clear();
            Username = "";

            successReads = 0; failReads = 0; successWrites = 0; failWrites = 0;
        }
    }



}