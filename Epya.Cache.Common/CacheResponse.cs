using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Common
{
    public class CacheResponse
    {
        public string ResponseCode { get; set; }
        public dynamic Body { get; set; }
        public Dictionary<string,string> Headers { get; set; }

        public string ErrorMessage {  get; set; }
        public string ErrorStackTrace { get; set; }
        public string FullErrorInfo { get; set; }


      

    }
}
