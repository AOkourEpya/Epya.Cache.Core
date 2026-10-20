using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Common
{
    public abstract class CacheBase
    {
        public CacheBase()
        {
            StringBuilder val = new StringBuilder();
            string typename = this.GetType().FullName.ToLower();

            foreach (char c in typename)
            {
                if (char.IsLower(c)) // Only process letters (a-z)
                {
                    int value = c - 'a' + 1;  // a=1, ..., z=26
                    val.Append(value);
                }
                else if (c == '.') // Resolve dot character
                {
                    val.Append("0"); // Use '0' as a separator for namespaces
                }

                val.Append(','); // Add separator
            }
            this.t = val.ToString().TrimEnd(',');


        }

        private string t;

        public string T
        {
            get
            {
                return t;
            }
        }

        public string TypeName
        {
            get
            {
                return this.GetType().FullName;
            }
        }
    }

}
