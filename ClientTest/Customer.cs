using Epya.Cache.Common;

namespace ClientTest
{
    public class Customer : CacheBase
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public List<Order> Orders { get; set; }
    }
}
