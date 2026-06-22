using Microsoft.EntityFrameworkCore;
using vless_proxy.Models;

public class DBContext : DbContext
{
    public DBContext(DbContextOptions<DBContext> options) : base(options) { }
   
    public DbSet<User> Users { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Client> Clients { get; set; }
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<Invite> Invites { get; set; }
    public DbSet<ApiKey> ApiKeys { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Username)
            .IsUnique();
        
        modelBuilder.Entity<Client>()
            .HasOne(c => c.Subscription)
            .WithMany()
            .HasForeignKey(c => c.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<Client>()
            .HasIndex(c => c.Slug)
            .IsUnique();

        modelBuilder.Entity<Client>()
            .HasIndex(c => c.SubscriptionId);
        
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.ClientId);

        modelBuilder.Entity<Device>()
            .HasIndex(d => new { d.ClientId, d.HWID });
    }
}