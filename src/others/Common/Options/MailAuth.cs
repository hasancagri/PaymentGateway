namespace Common.Options;

// Mail atan BC'nin Identity istemci kimliği (mail.send scope'lu client_credentials).
public class MailAuth
{
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
}
