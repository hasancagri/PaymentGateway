using PaymentGatewayApi.Modules.IAM.Roles;
using PaymentGatewayApi.Modules.IAM.Users;

namespace PaymentGatewayApi.Shared;

public static class DataSeeder
{
    private const string SuperAdminRoleName = "SuperAdmin";
    private const string MerchantAdminRoleName = "MerchantAdmin";

    public static void Seed(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IamContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        SeedRoles(db);
        db.SaveChanges();

        SeedSuperAdminUser(db, config);
        db.SaveChanges();
    }

    private static void SeedRoles(IamContext db)
    {
        var existingRoleNames = db.Set<Role>().AsEnumerable().Select(r => r.Name.Value).ToHashSet();

        if (!existingRoleNames.Contains(SuperAdminRoleName))
        {
            var role = Role.Create(SuperAdminRoleName, isSystem: true).Data!;
            AddSuperAdminPermissions(role);
            db.Set<Role>().Add(role);
        }

        if (!existingRoleNames.Contains(MerchantAdminRoleName))
        {
            var role = Role.Create(MerchantAdminRoleName, isSystem: true).Data!;
            AddMerchantAdminPermissions(role);
            db.Set<Role>().Add(role);
        }
    }

    private static void AddSuperAdminPermissions(Role role)
    {
        // Merchants
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Create);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Read);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Update);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Activate);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Deactivate);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Suspend);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsAdd);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsRemove);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysGenerate);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysRevoke);

        // Commissions
        role.AddPermission(CommissionPermissionConstants.Page, CommissionPermissionConstants.Create);
        role.AddPermission(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read);
        role.AddPermission(CommissionPermissionConstants.Page, CommissionPermissionConstants.Update);

        // Roles
        role.AddPermission(RolePermissionConstants.Page, RolePermissionConstants.Create);
        role.AddPermission(RolePermissionConstants.Page, RolePermissionConstants.Read);
        role.AddPermission(RolePermissionConstants.Page, RolePermissionConstants.PermissionsAdd);
        role.AddPermission(RolePermissionConstants.Page, RolePermissionConstants.PermissionsRemove);
    }

    private static void AddMerchantAdminPermissions(Role role)
    {
        // Merchants — create/activate/deactivate/suspend SuperAdmin'e ait
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Read);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.Update);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsAdd);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.BankAccountsRemove);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysGenerate);
        role.AddPermission(MerchantPermissionConstants.Page, MerchantPermissionConstants.ApiKeysRevoke);

        // Commissions — sadece okuma
        role.AddPermission(CommissionPermissionConstants.Page, CommissionPermissionConstants.Read);
    }

    private static void SeedSuperAdminUser(IamContext db, IConfiguration config)
    {
        var email = config["SuperAdmin:Email"]!;
        if (db.Set<User>().AsEnumerable().Any(u => u.Email.Value == email))
            return;

        var superAdminRole = db.Set<Role>().AsEnumerable().First(r => r.Name.Value == SuperAdminRoleName);

        var userResult = User.Create(
            email,
            config["SuperAdmin:Password"]!,
            config["SuperAdmin:FirstName"]!,
            config["SuperAdmin:LastName"]!);

        if (!userResult.IsSuccess) return;

        var user = userResult.Data!;
        user.AssignRole(superAdminRole.Id);
        db.Set<User>().Add(user);
    }
}