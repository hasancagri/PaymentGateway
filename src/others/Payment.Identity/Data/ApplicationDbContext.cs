using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Payment.Identity;

// Identity çekirdeği + OpenIddict store'ları (options.UseOpenIddict() Program'da bağlanır).
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options);