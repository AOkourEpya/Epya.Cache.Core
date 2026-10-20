using Epya.Cache.Common;

namespace ClientTest
{
    public class Product : CacheBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
    }
}
