namespace Payment.Api.Domains.StoreOnboarding;

/// <summary>
/// 091: Store onboarding müşteri yüzeyinin sunum katmanı — TEK sabit hosted formu string olarak inşa eder
/// (FR-003 — banka başına ayrı ekran YOK). Routing'den AYRI (endpoint ince kalır). Options/StoreName metni
/// HtmlEncode'lanır (XSS guard, İlke VI — sır render etmez). Mantık yok, sunum var. Form çekirdek profili JSON
/// gövdeyle POST /store-onboarding/{openToken}/submit eder.
/// </summary>
internal static class StoreOnboardingPages
{
    internal static string BuildOnboardingFormPage(string openToken, OpenOnboardingForm.OpenOnboardingFormResponse ctx, StoreOnboardingOptions options)
    {
        var title = System.Net.WebUtility.HtmlEncode(options.FormPageTitle);
        var intro = System.Net.WebUtility.HtmlEncode(options.FormPageIntro);
        var store = System.Net.WebUtility.HtmlEncode(ctx.StoreName);

        return $$"""
        <!doctype html>
        <html lang="tr">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{title}}</title>
          <style>
            *{box-sizing:border-box}
            body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;
              background:#f4f5f7;font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif;
              color:#1f2937;padding:24px}
            .card{background:#fff;border-radius:16px;box-shadow:0 10px 30px rgba(0,0,0,.08);padding:32px;max-width:460px;width:100%}
            h1{margin:0 0 6px;font-size:20px;font-weight:600}
            .intro{margin:0 0 20px;font-size:14px;color:#6b7280}
            .form{display:grid;gap:12px}
            .form label{font-size:12px;color:#6b7280;display:block;margin-bottom:4px}
            .form input{width:100%;padding:11px 12px;border:1px solid #d1d5db;border-radius:8px;font-size:15px}
            button{appearance:none;border:0;cursor:pointer;font-size:16px;font-weight:600;width:100%;
              margin-top:20px;padding:14px;border-radius:10px;background:#111827;color:#fff}
            button:disabled{background:#9ca3af;cursor:default}
            .msg{margin-top:16px;font-size:14px;text-align:center;display:none}
          </style>
        </head>
        <body>
          <div class="card">
            <h1>{{title}} — {{store}}</h1>
            <p class="intro">{{intro}}</p>
            <div class="form">
              <div><label>Yasal Ünvan</label><input id="legalName"></div>
              <div><label>Vergi No</label><input id="taxNumber"></div>
              <div><label>IBAN (TR...)</label><input id="iban"></div>
              <div><label>İşkolu (MCC, 4 hane)</label><input id="mcc"></div>
              <div><label>Web Sitesi</label><input id="websiteUrl"></div>
              <div><label>Yetkili Kişi</label><input id="contactName"></div>
              <div><label>Yetkili E-posta</label><input id="contactEmail"></div>
            </div>
            <button id="submit" onclick="send()">Başvuruyu Gönder</button>
            <div class="msg" id="msg"></div>
          </div>
          <script>
            async function send(){
              var btn=document.getElementById('submit'),msg=document.getElementById('msg');
              btn.disabled=true;msg.style.display='none';
              var body={
                legalName:val('legalName'),taxNumber:val('taxNumber'),iban:val('iban'),mcc:val('mcc'),
                websiteUrl:val('websiteUrl'),contactName:val('contactName'),contactEmail:val('contactEmail')
              };
              try{
                var r=await fetch('{{options.FormBaseUrl}}/{{openToken}}/submit',{
                  method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
                if(r.ok){msg.style.color='#16a34a';msg.textContent='Başvurun alındı, bankalara iletildi.';}
                else{msg.style.color='#dc2626';msg.textContent='Başvuru reddedildi. Alanları kontrol et.';btn.disabled=false;}
              }catch(e){msg.style.color='#dc2626';msg.textContent='Bağlantı hatası.';btn.disabled=false;}
              msg.style.display='block';
            }
            function val(id){return document.getElementById(id).value.trim();}
          </script>
        </body>
        </html>
        """;
    }
}
