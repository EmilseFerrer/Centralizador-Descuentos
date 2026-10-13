using Cazador.Api.Modules.Catalogo.Entities;
using Microsoft.EntityFrameworkCore;
using Cazador.Api.Modules.Participacion.Entities;

namespace Cazador.Api.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Fuente> Fuentes => Set<Fuente>();
    public DbSet<Oferta> Ofertas => Set<Oferta>();
    public DbSet<Beneficio> Beneficios => Set<Beneficio>();

    public DbSet<Apertura> Aperturas => Set<Apertura>();
    public DbSet<ReporteOferta> Reportes => Set<ReporteOferta>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Apertura>().Property(x => x.UrlDestino).HasMaxLength(2048);
        model.Entity<ReporteOferta>().Property(x => x.Motivo).HasMaxLength(30);
        model.Entity<Apertura>().HasOne(x => x.Oferta).WithMany().HasForeignKey(x => x.OfertaId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ReporteOferta>().HasOne(x => x.Oferta).WithMany().HasForeignKey(x => x.OfertaId).OnDelete(DeleteBehavior.Restrict);
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
