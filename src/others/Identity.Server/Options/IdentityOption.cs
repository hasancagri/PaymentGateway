namespace Identity.Server.Options;

// Aktivasyon istemcisi Identity token ucunun adresini kullanır. Config yoksa sabit issuer'a düşer
// (Program.cs SetIssuer ile aynı; D6). Common'a bağımlılık taşımamak için yerel POCO.
public class IdentityOption
{
    public string Address { get; set; } = "https://localhost:5101";
}
