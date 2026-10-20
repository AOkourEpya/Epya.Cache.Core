using Epya.Cache.Client;
using Microsoft.AspNetCore.Mvc;

namespace ClientTest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        public Customer Customer { get; set; }

        public WeatherForecastController()
        {
            List<Product> products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", Price = 10.99m },
                new Product { Id = 2, Name = "Product 2", Price = 19.99m },
                new Product { Id = 3, Name = "Product 3", Price = 5.99m }
            };
            List<Order> orders = new List<Order>
            {
                new Order { Id = 1, Date = DateTime.Now.AddDays(-7), Amount = 50.00m, Products = products },
                new Order { Id = 2, Date = DateTime.Now.AddDays(-3), Amount = 75.00m, Products = products }
            };
            Customer customer = new Customer
            {
                Id = 1,
                Name = "John Doe",
                Orders = orders
            };
            Customer = customer;
        }

        [HttpGet]
        [Cache]
        public Customer Get()
        {
           return Customer;
        }

        [HttpGet("AddCustomer")]
        public IActionResult AddOrder()
        {
            Order order = new Order
            {
                Id = 3,
                Date = DateTime.Now,
                Amount = 100.00m,
                Products = new List<Product>
                {
                    new Product { Id = 4, Name = "Product 4", Price = 29.99m },
                    new Product { Id = 5, Name = "Product 5", Price = 39.99m }
                }
            };

            Customer.Orders.Add(order);
            
            return Ok();
        }
    }
}
