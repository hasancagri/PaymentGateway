using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Commands;
using PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Queries;

namespace PaymentGatewayApi.Modules.Settlement.MerchantBalances.Features.Endpoints;

public static class MerchantBalanceEndpoints
{
    private record RejectRequest(string Reason);

    public static IEndpointRouteBuilder MapMerchantBalanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/balances");

        group.MapPost("/", async ([FromBody] CreateMerchantBalance.CreateMerchantBalanceCommand cmd, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<CreateMerchantBalance.CreateMerchantBalanceResponse>>(
                    cmd);
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapGet("/{merchantId:guid}", async (Guid merchantId, IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantBalance.GetMerchantBalanceResponse>>(
                    new GetMerchantBalance.GetMerchantBalanceQuery { MerchantId = merchantId });
            return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/{merchantId:guid}/credit",
            async ([FromBody] CreditMerchantBalance.CreditMerchantBalanceCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<FeatureObjectResultModel<
                            CreditMerchantBalance.CreditMerchantBalanceCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{merchantId:guid}/debit",
            async ([FromBody] DebitMerchantBalance.DebitMerchantBalanceCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus
                        .InvokeAsync<
                            FeatureObjectResultModel<DebitMerchantBalance.DebitMerchantBalanceCommandResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{merchantId:guid}/withdrawals",
            async ([FromBody] RequestWithdrawal.RequestWithdrawalCommand cmd, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<RequestWithdrawal.RequestWithdrawalResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{merchantId:guid}/withdrawals/{withdrawalId:guid}/approve",
            async (Guid merchantId, Guid withdrawalId, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<ApproveWithdrawal.ApproveWithdrawalCommandResponse>>(
                        new ApproveWithdrawal.ApproveWithdrawalCommand
                            { MerchantId = merchantId, WithdrawalId = withdrawalId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{merchantId:guid}/withdrawals/{withdrawalId:guid}/reject",
            async (Guid merchantId, Guid withdrawalId, [FromBody] RejectRequest req, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<RejectWithdrawal.RejectWithdrawalCommandResponse>>(
                        new RejectWithdrawal.RejectWithdrawalCommand
                            { MerchantId = merchantId, WithdrawalId = withdrawalId, Reason = req.Reason });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        group.MapPost("/{merchantId:guid}/withdrawals/{withdrawalId:guid}/process",
            async (Guid merchantId, Guid withdrawalId, IMessageBus bus) =>
            {
                var result =
                    await bus.InvokeAsync<FeatureObjectResultModel<ProcessWithdrawal.ProcessWithdrawalCommandResponse>>(
                        new ProcessWithdrawal.ProcessWithdrawalCommand
                            { MerchantId = merchantId, WithdrawalId = withdrawalId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            });

        return app;
    }
}