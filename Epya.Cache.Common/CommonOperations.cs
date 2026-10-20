using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Common
{
    public static class CommonOperations
    {
 
        public static bool IsValidJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                var token = JToken.Parse(input);

                // Exclude empty objects ({}) and empty arrays ([])
                if ((token is JObject obj && !obj.HasValues) ||
                    (token is JArray array && array.Count == 0))
                {
                    return false;
                }

                return true;
            }
            catch (JsonReaderException)
            {
                return false; // Invalid JSON
            }
        }

        public static bool IsValidJsonAndHasT(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                var token = JToken.Parse(input);

                // Exclude empty objects ({}) and empty arrays ([])
                if ((token is JObject obj && !obj.HasValues) ||
                    (token is JArray array && array.Count == 0))
                {
                    return false;
                }

                return ContainsPropertyT(token);
            }
            catch (JsonReaderException)
            {
                return false; // Invalid JSON
            }
        }

        private static bool ContainsPropertyT(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (string.Equals(property.Name, "t", StringComparison.OrdinalIgnoreCase))
                        return true;

                    //if (property.Value is JArray array)
                    //    if (ContainsPropertyT(property.Value))
                    //        return true;
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                {
                    if (ContainsPropertyT(item))
                        return true;
                }
            }

            return false;
        }
    }
}
