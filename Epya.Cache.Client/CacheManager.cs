using Epya.Cache.Common;
using Epya.Cache.Common.Tcp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Epya.Cache.Client
{
    public class CacheManager
    {
        private static readonly ArrayPool<byte> BufferPool = ArrayPool<byte>.Create(65536, 300);// 64 KB buffers, 100 max

        private readonly CacheConfigDto configs;

        private string serverAddress;
        private int readPort;
        private int writePort;

        private static bool isMetaDataSent = false;
        private static List<EntityMetadataDto> contextMetaData;

        /// <summary>
        /// Resolves the configuration published by AddEpyaCache/UseEpyaCache. Prefer the
        /// <see cref="CacheManager(CacheConfigDto)"/> overload wherever a container is at hand.
        /// </summary>
        public CacheManager() : this(Extensions.Current) { }

        public CacheManager(CacheConfigDto configs) {

            // A missing section leaves every flag off, which is how the old code behaved
            // when ConfigurationManager returned no cacheSettings section.
            this.configs = configs ?? new CacheConfigDto();

            try
            {

                if (IsChacheEnabled())
                {
                    if (!IsChacheConfigured())
                        throw new Exception("ServerAddress, ReadPort or OthersPort is Null");

                    serverAddress = this.configs.ServerAddress;
                    readPort = this.configs.ReadPort;
                    writePort = this.configs.OthersPort;

                    //_readPool = new TcpClientPool(this.configs, readPort, maxPoolSize: readPoolSize);
                    //_writePool = new TcpClientPool(this.configs, writePort, maxPoolSize: writePoolSize);
                }
            }
            catch(Exception ex) {
                _logExceptions(ex);
                if (IsChacheEnabled())
                    throw;
            }
        }


        public bool IsChacheEnabled()
        {
            return configs.IsEnabled;
        }
        public bool IsChacheConfigured()
        {
            return configs.IsConfigured;
        }
        public bool IsChacheEnabledAndConfigured()
        {
            return configs.IsEnabledAndConfigured;
        }

        public List<EntityMetadataDto> GetContextMetadata()
        {
            try
            {
                if (isMetaDataSent)
                    return contextMetaData;
                return null;
            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                throw;
            }
        }
        public async Task<CacheResponse> SendReadRequestAsync(CacheRequest request)
        {
            try
            {
                if (!IsChacheEnabledAndConfigured())
                    return _BuildExecptionCacheResponse("the Cache is not enabled or Not Cofigured Correctly as ServerAddress, ReadPort or OthersPort is Null");


                if (request == null)
                    return _BuildExecptionCacheResponse("CacheRequest is Empty");

                request.Action = Common.Action.Read;

                string message = JsonConvert.SerializeObject(request);
                TcpManager tcpManager = new TcpManager(serverAddress, readPort);
                string res = await tcpManager.SendAsync(message);

                CacheResponse cacheResponse = null;
                if (CommonOperations.IsValidJson(res))
                    cacheResponse = JsonConvert.DeserializeObject<CacheResponse>(res);
                else
                    cacheResponse = _BuildExecptionCacheResponse("Invalid JSON response");

                return cacheResponse;
            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                return _BuildExecptionCacheResponse(ex);
            }
        }
        public async Task<CacheResponse> SendWriteRequestAsync(CacheRequest request)
        {
            try
            {
                if (!IsChacheEnabledAndConfigured())
                    return _BuildExecptionCacheResponse("the Cache is not enabled or Not Cofigured Correctly as ServerAddress, ReadPort or OthersPort is Null");


                if (request == null)
                    return _BuildExecptionCacheResponse("CacheRequest is Empty");

                request.Action = Common.Action.Write;

                string message = JsonConvert.SerializeObject(request);
                TcpManager tcpManager = new TcpManager(serverAddress, writePort);
                string res = await tcpManager.SendAsync(message);

                CacheResponse cacheResponse = null;
                if (CommonOperations.IsValidJson(res))
                    cacheResponse = JsonConvert.DeserializeObject<CacheResponse>(res);
                else
                    cacheResponse = _BuildExecptionCacheResponse("Invalid JSON response");

                return cacheResponse;

            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                return _BuildExecptionCacheResponse(ex);
            }

        }
        public async Task<CacheResponse> SendCheckImpactRequestAsync(CheckImpactRequest request)
        {
            try
            {
                if (!IsChacheEnabledAndConfigured())
                    return _BuildExecptionCacheResponse("the Cache is not enabled or Not Cofigured Correctly as ServerAddress, ReadPort or OthersPort is Null");


                if (request == null)
                    return _BuildExecptionCacheResponse("CheckImpactRequest is Empty");


                CacheRequest req = new CacheRequest();
                req.Body = JsonConvert.SerializeObject(request.EntityActionMetadata);
                req.Action = Common.Action.CheckImpact;

                string message = JsonConvert.SerializeObject(req);
                TcpManager tcpManager = new TcpManager(serverAddress, writePort);
                string res = await tcpManager.SendAsync(message);

                CacheResponse cacheResponse = null;
                if (CommonOperations.IsValidJson(res))
                    cacheResponse = JsonConvert.DeserializeObject<CacheResponse>(res);
                else
                    cacheResponse = _BuildExecptionCacheResponse("Invalid JSON response");

                return cacheResponse;

            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                return _BuildExecptionCacheResponse(ex);
            }
        }
        public async Task<CacheResponse> GenerateContextMetadataAsync(DbContext context,bool force=false)
        {
            try
            {
                if (!IsChacheEnabledAndConfigured())
                    return _BuildExecptionCacheResponse("the Cache is not enabled or Not Cofigured Correctly as ServerAddress, ReadPort or OthersPort is Null");


                CacheResponse cacheResponse;

                if (force)
                    isMetaDataSent = false;

                if (!isMetaDataSent)
                {
                    EntityMetadataRequest request = new EntityMetadataRequest();
                    contextMetaData = _GenerateDBContextMetaData(context);
                    request.Entities = contextMetaData;

                    CacheRequest req = new CacheRequest();
                    req.Body = JsonConvert.SerializeObject(request.Entities);
                    req.Action = Common.Action.ExportMetadata;

                    string message = JsonConvert.SerializeObject(req);
                    TcpManager tcpManager = new TcpManager(serverAddress, writePort);
                    string res = await tcpManager.SendAsync(message);

                    if (CommonOperations.IsValidJson(res))
                        cacheResponse = JsonConvert.DeserializeObject<CacheResponse>(res);
                    else
                        cacheResponse = _BuildExecptionCacheResponse("Invalid JSON response");

                    if (cacheResponse.ResponseCode == "200")
                        isMetaDataSent = true;
                }
                else
                {
                    //metadata already send to the cache server
                    cacheResponse = _BuildExecptionCacheResponse("the Metadata sent before");
                }
                return cacheResponse;
            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                return _BuildExecptionCacheResponse(ex);
            }
        }
        public CacheResponse GenerateContextMetadata(DbContext context, bool force = false)
        {
            try
            {
                if (!IsChacheEnabledAndConfigured())
                    return _BuildExecptionCacheResponse("the Cache is not enabled or Not Cofigured Correctly as ServerAddress, ReadPort or OthersPort is Null");


                CacheResponse cacheResponse;

                if (force)
                    isMetaDataSent = false;

                if (!isMetaDataSent)
                {
                    EntityMetadataRequest request = new EntityMetadataRequest();
                    contextMetaData = _GenerateDBContextMetaData(context);
                    request.Entities = contextMetaData;

                    CacheRequest req = new CacheRequest();
                    req.Body = JsonConvert.SerializeObject(request.Entities)    ;
                    req.Action = Common.Action.ExportMetadata;

                    string message = JsonConvert.SerializeObject(req);
                    TcpManager tcpManager = new TcpManager(serverAddress, writePort);
                    string res = tcpManager.Send(message);

                    if (CommonOperations.IsValidJson(res))
                        cacheResponse = JsonConvert.DeserializeObject<CacheResponse>(res);
                    else
                        cacheResponse = _BuildExecptionCacheResponse("Invalid JSON response");

                    if (cacheResponse.ResponseCode == "200")
                        isMetaDataSent = true;
                }
                else
                {
                    //metadata already send to the cache server
                    cacheResponse = _BuildExecptionCacheResponse("the Metadata sent before");
                }
                return cacheResponse;
            }
            catch (Exception ex)
            {
                _logExceptions(ex);
                return _BuildExecptionCacheResponse(ex);
            }
        }
        public string DecodeEntityTypeName(string encodedEntityname)
        {
            string[] parts = encodedEntityname.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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

        private List<EntityMetadataDto> _GenerateDBContextMetaData(DbContext context)
        {
            try
            {
                if (context == null)
                    throw new Exception("the Provided BDContext is Null");

                var allEntities = new List<EntityMetadataDto>();

                foreach (IEntityType entityType in context.Model.GetEntityTypes())
                {
                    // Property-bag types are EF Core's implicit many-to-many join entities.
                    // They have no CLR identity for the cache to key dependencies on.
                    if (entityType.IsPropertyBag)
                        continue;

                    Type clrType = entityType.ClrType;

                    var entityDto = new EntityMetadataDto
                    {
                        EntityName = clrType.FullName
                    };

                    ForeignKeyMetadataDto Describe(IReadOnlyNavigationBase nav, string fkProperty, bool isRequired)
                    {
                        IReadOnlyNavigationBase inverse = nav.Inverse;
                        PropertyInfo navPropInfo = nav.PropertyInfo ?? clrType.GetProperty(nav.Name);
                        Type relatedClrType = nav.TargetEntityType.ClrType;

                        return new ForeignKeyMetadataDto
                        {
                            ForeignKeyProperty = fkProperty ?? "Unknown",
                            RelatedEntity = relatedClrType.Name,
                            RelatedEntityTypeFullName = relatedClrType.FullName,
                            NavigationProperty = nav.Name,
                            NavigationPropertyTypeFullName = navPropInfo?.PropertyType?.FullName ?? "Unknown",
                            IsRequired = isRequired,
                            RelationType = nav.IsCollection ? "Collection" : "Single",

                            // EF Core pairs navigations directly, so the inverse no longer has to
                            // be inferred by matching FK names across entities as the EF6 code did.
                            InverseCollectionNavigation =
                                (!nav.IsCollection && inverse != null && inverse.IsCollection)
                                    ? inverse.Name
                                    : null
                        };
                    }

                    foreach (INavigation nav in entityType.GetNavigations())
                    {
                        IForeignKey fk = nav.ForeignKey;

                        entityDto.ForeignKeys.Add(Describe(
                            nav,
                            fk.Properties.Count > 0 ? fk.Properties[0].Name : null,
                            !nav.IsCollection && (nav.IsOnDependent ? fk.IsRequired : fk.IsRequiredDependent)));
                    }

                    // Many-to-many is modelled as skip navigations, which carry no foreign key
                    // of their own - the EF6 version reported those with an unresolved FK too.
                    foreach (ISkipNavigation nav in entityType.GetSkipNavigations())
                    {
                        entityDto.ForeignKeys.Add(Describe(nav, null, false));
                    }

                    if (entityDto.ForeignKeys.Any())
                        allEntities.Add(entityDto);
                }

                if (allEntities.Count == 0)
                    return null;

                return allEntities;
            }
            catch
            {
                throw;
            }
        }
        private CacheResponse _BuildExecptionCacheResponse(string message)
        {
            _logExceptions(message);

            return new CacheResponse
            {
                ResponseCode = "500",
                ErrorMessage = message
            };
        }
        private CacheResponse _BuildExecptionCacheResponse(Exception ex)
        {
            var fullMessageBuilder = new System.Text.StringBuilder();
            var currentEx = ex;
            int level = 0;

            while (currentEx != null)
            {
                fullMessageBuilder
                    .AppendLine($"Level {level}")
                    .AppendLine($"Message: {currentEx.Message}")
                    .AppendLine($"StackTrace: {currentEx.StackTrace}")
                    .AppendLine();

                currentEx = currentEx.InnerException;
                level++;
            }

            _logExceptions(fullMessageBuilder.ToString());

            return new CacheResponse
            {
                ResponseCode = "500",
                ErrorMessage = ex.Message,
                FullErrorInfo = fullMessageBuilder.ToString(),
                ErrorStackTrace = ex.StackTrace
            };
        }


        private void _logExceptions(string message)
        {

        }
        private void _logExceptions(Exception ex)
        {

        }
    }

}
