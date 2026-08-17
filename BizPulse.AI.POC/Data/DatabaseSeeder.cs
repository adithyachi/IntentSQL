using BizPulse.AI.POC.Models;

namespace BizPulse.AI.POC.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        if (context.Categories.Any())
        {
            return;
        }

        var random = new Random(42);

        // -----------------------------
        // Categories
        // -----------------------------

        var categoryNames = new[]
        {
            "Electronics",
            "Computers",
            "Mobile Phones",
            "Accessories",
            "Home Appliances",
            "Office Supplies",
            "Furniture",
            "Gaming",
            "Audio",
            "Networking"
        };

        var categories = categoryNames
            .Select(name => new Category
            {
                Name = name
            })
            .ToList();

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        // -----------------------------
        // Products
        // -----------------------------

        var productPrefixes = new[]
        {
            "Pro",
            "Ultra",
            "Elite",
            "Smart",
            "Advanced",
            "Premium",
            "Essential",
            "Max",
            "Plus",
            "Standard"
        };

        var productTypes = new[]
        {
            "Laptop",
            "Monitor",
            "Keyboard",
            "Mouse",
            "Headphones",
            "Phone",
            "Tablet",
            "Router",
            "Speaker",
            "Camera"
        };

        var products = new List<Product>();

        for (var i = 1; i <= 100; i++)
        {
            var category = categories[(i - 1) % categories.Count];

            var prefix = productPrefixes[(i - 1) % productPrefixes.Length];

            var type = productTypes[(i - 1) % productTypes.Length];

            var price = Math.Round(
                (decimal)(random.Next(50, 2000) + random.NextDouble()),
                2);

            var costPrice = Math.Round(
                price * (decimal)(0.45 + random.NextDouble() * 0.25),
                2);

            products.Add(new Product
            {
                Name = $"{prefix} {type} {i}",
                CategoryId = category.Id,
                Price = price,
                CostPrice = costPrice
            });
        }

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();

        // -----------------------------
        // Customers
        // -----------------------------

        var firstNames = new[]
        {
            "Arjun",
            "Priya",
            "Rahul",
            "Ananya",
            "Vikram",
            "Sneha",
            "Kiran",
            "Neha",
            "Rohit",
            "Meera",
            "Amit",
            "Pooja",
            "Sanjay",
            "Divya",
            "Nikhil"
        };

        var lastNames = new[]
        {
            "Sharma",
            "Patel",
            "Reddy",
            "Kumar",
            "Singh",
            "Rao",
            "Gupta",
            "Verma",
            "Iyer",
            "Nair"
        };

        var cities = new[]
        {
            "Hyderabad",
            "Bangalore",
            "Chennai",
            "Mumbai",
            "Delhi",
            "Pune",
            "Kolkata",
            "Ahmedabad",
            "Jaipur",
            "Vijayawada"
        };

        var customers = new List<Customer>();

        for (var i = 1; i <= 500; i++)
        {
            var firstName = firstNames[(i - 1) % firstNames.Length];

            var lastName = lastNames[(i - 1) % lastNames.Length];

            customers.Add(new Customer
            {
                Name = $"{firstName} {lastName} {i}",
                Email = $"customer{i}@example.com",
                City = cities[(i - 1) % cities.Length],
                Country = "India"
            });
        }

        await context.Customers.AddRangeAsync(customers);
        await context.SaveChangesAsync();

        // -----------------------------
        // Orders
        // -----------------------------

        var statuses = new[]
        {
            "Completed",
            "Completed",
            "Completed",
            "Completed",
            "Cancelled",
            "Pending"
        };

        var orders = new List<Order>();

        var startDate = new DateTime(
            2025,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        for (var i = 1; i <= 5000; i++)
        {
            var customer = customers[random.Next(customers.Count)];

            var days = random.Next(0, 365);

            orders.Add(new Order
            {
                CustomerId = customer.Id,
                OrderDate = startDate.AddDays(days),
                Status = statuses[random.Next(statuses.Length)]
            });
        }

        await context.Orders.AddRangeAsync(orders);
        await context.SaveChangesAsync();

        // -----------------------------
        // Order Items
        // -----------------------------

        var orderItems = new List<OrderItem>();

        foreach (var order in orders)
        {
            var itemCount = random.Next(1, 5);

            var selectedProducts = products
                .OrderBy(_ => random.Next())
                .Take(itemCount)
                .ToList();

            foreach (var product in selectedProducts)
            {
                var quantity = random.Next(1, 5);

                orderItems.Add(new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = product.Price
                });
            }
        }

        await context.OrderItems.AddRangeAsync(orderItems);

        await context.SaveChangesAsync();
    }
}