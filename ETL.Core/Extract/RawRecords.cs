using CsvHelper.Configuration.Attributes;

namespace ETL.Core.Extract
{
    public class CustomerCsv
    {
        [Name("CustomerID")] public int CustomerID { get; set; }
        [Name("FirstName")] public string FirstName { get; set; } = string.Empty;
        [Name("LastName")] public string LastName { get; set; } = string.Empty;
        [Name("Email")] public string? Email { get; set; }
        [Name("Phone")] public string? Phone { get; set; }
        [Name("City")] public string? City { get; set; }
        [Name("Country")] public string? Country { get; set; }
    }

    public class ProductCsv
    {
        [Name("ProductID")] public int ProductID { get; set; }
        [Name("ProductName")] public string ProductName { get; set; } = string.Empty;
        [Name("Category")] public string Category { get; set; } = string.Empty;
        [Name("Price")] public decimal Price { get; set; }
        [Name("Stock")] public int Stock { get; set; }
    }

    public class OrderCsv
    {
        [Name("OrderID")] public int OrderID { get; set; }
        [Name("CustomerID")] public int CustomerID { get; set; }
        [Name("OrderDate")] public DateTime OrderDate { get; set; }
        [Name("Status")] public string Status { get; set; } = string.Empty;
    }

    public class OrderDetailCsv
    {
        [Name("OrderID")] public int OrderID { get; set; }
        [Name("ProductID")] public int ProductID { get; set; }
        [Name("Quantity")] public int Quantity { get; set; }
        [Name("TotalPrice")] public decimal TotalPrice { get; set; }
    }
}