namespace Admin.Options;

// Admin BFF yalnız Identity token ucunun adresini kullanır (client_credentials).
// Common'a bağımlılık taşımamak için yerel POCO (Common.Options.IdentityOption paritesi).
public class IdentityOption
{
    public required string Address { get; set; }
}
