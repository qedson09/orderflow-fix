namespace OrderFlow.Domain.Customers;

public static class CustomerCatalog
{
    public static IReadOnlyList<Customer> All { get; } = Array.AsReadOnly([
        new Customer { Id = "CLIENTE-001", Name = "Cliente demonstração 1" },
        new Customer { Id = "CLIENTE-002", Name = "Cliente demonstração 2" }
    ]);

    public static bool Contains(string id) => All.Any(x => x.Id == id);
}
