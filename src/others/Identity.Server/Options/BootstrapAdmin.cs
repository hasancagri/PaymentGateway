namespace Identity.Server.Options;

/// <summary>
/// G3: Açılışta seed edilen tek admin kimliği — section "BootstrapAdmin". Email/parola boşken
/// (config'te tanımsız) kullanıcı oluşturma atlanır; bu yüzden alanlar zorunlu (Required) değil.
/// Placeholder: kullanıcı `dotnet user-secrets set BootstrapAdmin:Email/Password` ile kendi
/// değerini girer.
/// </summary>
public class BootstrapAdmin
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}
