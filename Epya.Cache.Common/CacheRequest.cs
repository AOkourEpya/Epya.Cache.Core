using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Common
{
    public class CacheRequest
    {
        public string FullUrl { get; set; }
        //public string Verb { get; set; }
        public string UserName { get; set; }
        public dynamic Body { get; set; }
        public Dictionary<string, string> Headers { get; set; }

        public Action Action { get; set; }

        [JsonIgnore]
        public string CacheKey
        {
            get
            {
                return FullUrl + ":" + UserName;
            }
        }
    }

    public class CheckImpactRequest
    {
        public List<KeyValuePair<string, string>> Types { get; set; }
        public List<EntityActionMetadata> EntityActionMetadata { get; set; }
    }

    public class EntityActionMetadata
    {
        public string Type;
        public string Id;
        public EntityOperation Operation= EntityOperation.None;
        public Dictionary<string,string> Fks { get; set; }

    }

    public class EntityMetadataRequest
    {
        public List<EntityMetadataDto> Entities { get; set; }
    }


    public enum Action
    {
        Read =1,
        Write=2,
        CheckImpact=3,
        ExportMetadata = 4
    }


    public enum EntityOperation
    {
        None = 0,
        Add = 1,
        Update = 2,
        Delete = 3,
    }

}
