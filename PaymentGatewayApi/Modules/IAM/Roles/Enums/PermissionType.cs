namespace PaymentGatewayApi.Modules.IAM.Roles.Enums;

public enum PermissionType
{
    Read = 1,
    Write = 2,
    Update = 3,
    Delete = 4,
    PageAccess = 5, // sayfa bazlı
    ButtonAccess = 6 // buton bazlı    
}