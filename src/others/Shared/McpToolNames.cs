namespace Shared;

// 043: admin-facing MCP tool adları — dış sözleşim (integration-event kontratı emsali), sabit
// olması rename'i derleme hatasıyla yakalar (research.md #10). [McpServerTool(Name = ...)] bu
// sabitlerden okunur, literal YOK.
public static class MerchantAdminTools
{
    public const string ActivateMerchant = "admin_activate_merchant";
    public const string DeactivateMerchant = "admin_deactivate_merchant";
    public const string SuspendMerchant = "admin_suspend_merchant";
    public const string GetMerchants = "admin_get_merchants";
    public const string GetPendingRegistrations = "admin_get_pending_registrations";
    public const string ApproveRegistration = "admin_approve_registration";
    public const string RejectRegistration = "admin_reject_registration";
}

public static class CommissionAdminTools
{
    public const string GetCommissionPolicy = "admin_get_commission_policy";
}