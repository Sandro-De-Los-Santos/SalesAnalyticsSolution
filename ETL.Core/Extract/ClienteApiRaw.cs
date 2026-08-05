namespace ETL.Core.Extract;

public class ClienteApiRaw
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public AddressRaw? Address { get; set; }
}

public class AddressRaw
{
    public string City { get; set; } = string.Empty;
}