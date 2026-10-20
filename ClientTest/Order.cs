using Epya.Cache.Common;

namespace ClientTest
{
    public class Order : CacheBase
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public List<Product> Products { get; set; }
    }
}
