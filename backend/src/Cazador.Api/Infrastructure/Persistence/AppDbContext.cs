using Cazador.Api.Modules.Catalogo.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cazador.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Fuente> Fuentes => Set<Fuente>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<Beneficio> Beneficios => Set<Beneficio>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Fuente>().Property(x => x.Marca).HasMaxLength(80);
        model.Entity<Fuente>().Property(x => x.Url).HasMaxLength(2048);
        model.Entity<Oferta>().Property(x => x.IdentificadorPublicacion).HasMaxLength(255);
        model.Entity<Oferta>().HasIndex(x => new { x.FuenteId, x.IdentificadorPublicacion }).IsUnique();
        model.Entity<Oferta>().Property(x => x.Nombre).HasMaxLength(300);
        model.Entity<Oferta>().Property(x => x.UrlOriginal).HasMaxLength(2048);
        model.Entity<Beneficio>().Property(x => x.Tipo).HasConversion<string>().HasMaxLength(40);
        model.Entity<Beneficio>().Property(x => x.Valor).HasPrecision(18, 2);
        model.Entity<Beneficio>().Property(x => x.Moneda).HasMaxLength(10);
    }
}
