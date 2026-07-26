using LeadScoutCRM.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LeadScoutCRM.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Lead> Leads { get; set; }
    public DbSet<Note> Notes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); 

        // Índice único no GooglePlaceId por utilizador
        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.GooglePlaceId, l.UserId })
            .IsUnique()
            .HasFilter("[GooglePlaceId] IS NOT NULL");

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Lead)
            .WithMany(l => l.Notes)
            .HasForeignKey(n => n.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Lead>()
            .Property(l => l.Status)
            .HasConversion<string>();

        // Enum string no BD
        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.Plan)
            .HasConversion<string>();

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.SubscriptionStatus)
            .HasConversion<string>();
    }
}