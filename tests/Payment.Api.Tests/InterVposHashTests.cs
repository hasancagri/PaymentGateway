using Payment.Api.Domains.PaymentLinks.BankCharge;

namespace Payment.Api.Tests;

// Phase-1 3DS (İlke VI): PG-tarafı InterVpos hash. İstek hash'i = sim `InterVposHash` ile birebir (ayraçsız
// birleştirme + SHA1 base64); callback hash'i = değerler ':' + SecretKey. Determinist + sim-paritesi kilidi.
public class InterVposHashTests
{
    [Fact]
    public void RequestHash_is_deterministic_and_order_sensitive()
    {
        var a = InterVposHash.ComputeRequestHash("MAVI0001", "ORD1", "100.00", "ok", "fail", "Auth", "", "RND", "SECRET");
        var b = InterVposHash.ComputeRequestHash("MAVI0001", "ORD1", "100.00", "ok", "fail", "Auth", "", "RND", "SECRET");
        var c = InterVposHash.ComputeRequestHash("MAVI0001", "ORD2", "100.00", "ok", "fail", "Auth", "", "RND", "SECRET");

        Assert.Equal(a, b);          // aynı girdi → aynı hash
        Assert.NotEqual(a, c);       // farklı OrderId → farklı hash (manipülasyon yakalanır)
    }

    [Fact]
    public void CallbackHash_matches_known_concatenation()
    {
        // base64(SHA1("1:MD:00SECRET")) — sim ComputeCallbackHash aynı formülü üretir.
        var expected = Convert.ToBase64String(
            System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes("1:MD:00SECRET")));
        var actual = InterVposHash.ComputeCallbackHash(new[] { "1", "MD", "00" }, "SECRET");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void VerifyCallback_accepts_signed_and_rejects_tampered()
    {
        var adapter = new InterVposBankAdapter(new Payment.Api.Options.InterVposBankProfile
        {
            BankCode = "MAVIBANK", BaseUrl = "http://x", ShopCode = "MAVI0001", UserCode = "U", UserPass = "P", SecretKey = "SECRET"
        });
        var signed = new (string Name, string Value)[] { ("3DStatus", "1"), ("OrderId", "ORD1"), ("ProcReturnCode", "00") };
        var hash = InterVposHash.ComputeCallbackHash(signed.Select(x => x.Value), "SECRET");
        var callback = signed.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        callback["HASHPARAMS"] = string.Join(":", signed.Select(x => x.Name));
        callback["HASH"] = hash;

        Assert.True(adapter.VerifyCallback(callback));          // banka imzası geçerli

        callback["ProcReturnCode"] = "51";                      // değer kurcalandı
        Assert.False(adapter.VerifyCallback(callback));         // imza artık tutmaz → fail-closed
    }
}
