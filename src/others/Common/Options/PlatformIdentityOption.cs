namespace Common.Options;

// 047: MCP yüzeyinin KİMLİK otoritesi — AgentPlatform IdP (insan/agent düzlemi). REST tarafı PG
// IdP'de (IdentityOption) kalır → yüzey başına tek otorite (FR-005). "Platform" JwtBearer şemasını
// besler; Audience BC'nin kendi adı (merchant.api / commission.api).
public class PlatformIdentityOption
{
    public required string Address { get; set; }
    public required string Audience { get; set; }
}
