using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ApiGateway;
using StackExchange.Redis;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var rabbitMq = builder.Configuration.GetConnectionString("rabbitmq");
var redisConn = builder.Configuration.GetConnectionString("redis");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

builder.Services.AddSingleton<JwtService>();

builder.Host.UseWolverine(opts =>
{
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());

    if (!string.IsNullOrEmpty(rabbitMq))
    {
        var transport = opts.UseRabbitMq(new Uri(rabbitMq)).AutoProvision();

        // Bind each fanout exchange to a single gateway queue
        const string gatewayQueue = "gateway.merchant-events";
        transport
            .BindExchange("merchant.created", ExchangeType.Fanout)
            .ToQueue(gatewayQueue);
        transport
            .BindExchange("merchant.api-key-generated", ExchangeType.Fanout)
            .ToQueue(gatewayQueue);
        transport
            .BindExchange("merchant.api-key-revoked", ExchangeType.Fanout)
            .ToQueue(gatewayQueue);
        transport
            .BindExchange("merchant.status-changed", ExchangeType.Fanout)
            .ToQueue(gatewayQueue);

        opts.ListenToRabbitQueue(gatewayQueue);
    }
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapDefaultEndpoints();

app.MapReverseProxy(pipeline =>
{
    pipeline.Use(async (ctx, next) =>
    {
        var apiKey = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(apiKey))
        {
            var jwtSvc = ctx.RequestServices.GetRequiredService<JwtService>();
            var redis = ctx.RequestServices.GetService<IConnectionMultiplexer>();

            if (redis is null)
            {
                ctx.Response.StatusCode = 503;
                return;
            }

            var db = redis.GetDatabase();
            var keyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
            var merchantId = await db.HashGetAsync($"apikey:{keyHash}", "merchant_id");
            var merchantName = await db.HashGetAsync($"apikey:{keyHash}", "merchant_name");
            var status = await db.HashGetAsync($"apikey:{keyHash}", "status");

            if (!merchantId.IsNull && status == "Active")
            {
                var token = jwtSvc.GenerateToken(Guid.Parse((string)merchantId!), (string)merchantName!);
                ctx.Request.Headers["Authorization"] = $"Bearer {token}";
                ctx.Request.Headers.Remove("X-Api-Key");
            }
            else
            {
                ctx.Response.StatusCode = 401;
                return;
            }
        }

        await next(ctx);
    });
});

app.Run();