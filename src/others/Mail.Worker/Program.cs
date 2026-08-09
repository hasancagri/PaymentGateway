using System.Net.Mail;
using Microsoft.Extensions.Options;
using Shared;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// 016: SMTP ayarları POCO (runtime doğrudan IConfiguration okuması yasak; CLAUDE.md).
builder.Services.AddOptions<Mail.Worker.Options.Mail>().BindConfiguration("Mail")
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<Mail.Worker.Options.Mail>(sp =>
    sp.GetRequiredService<IOptions<Mail.Worker.Options.Mail>>().Value);

// 016: Mail.Worker = düz mail projesi (MCP DEĞİL). mail.delivery fanout'unu durable queue ile tüketir;
// SendEmailHandler SMTP ile gönderir. SMTP geçici hatası → backoff'lu retry; tükenirse dead-letter.
builder.Host.UseWolverine(opts =>
{
    // Dev: tek düğüm (Solo). Message store yok → durable inbox kullanılamaz; kuyruk RabbitMQ tarafında
    // durable, işleme inline (ack handler bitince). Retry bellek-içi + RabbitMQ redelivery.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    var rabbit = opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    rabbit.DeclareExchange(RabbitMqConstants.MailDelivery.Exchange,
        e => { e.ExchangeType = ExchangeType.Fanout; });
    rabbit.DeclareQueue(RabbitMqConstants.MailDelivery.WorkerQueue);
    rabbit.BindExchange(RabbitMqConstants.MailDelivery.Exchange)
        .ToQueue(RabbitMqConstants.MailDelivery.WorkerQueue);

    opts.ListenToRabbitQueue(RabbitMqConstants.MailDelivery.WorkerQueue).ProcessInline();

    // Retry: SMTP geçici hata (Mailpit down / geçici ağ) artan backoff'la yeniden denenir; tümü
    // tükenirse mesaj dead-letter'a taşınır (kayıp yok, elle incelenir).
    opts.Policies.OnException<SmtpException>()
        .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15))
        .Then.MoveToErrorQueue();

    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.ServiceLocationPolicy = JasperFx.CodeGeneration.Model.ServiceLocationPolicy.AllowedButWarn;
});

var app = builder.Build();

app.MapDefaultEndpoints();

await app.RunAsync();