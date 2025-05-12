using Chamsoc.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Chamsoc.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Senior> Seniors { get; set; }
        public DbSet<Caregiver> Caregivers { get; set; }
        public DbSet<CareJob> CareJobs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<Rating> Ratings { get; set; }

        public string Contact { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Call the base OnModelCreating to apply Identity configurations
            base.OnModelCreating(modelBuilder);

            // Ensure IsVerified is mapped as a BIT column
            modelBuilder.Entity<Senior>()
                .Property(s => s.IsVerified)
                .HasColumnType("bit");

            modelBuilder.Entity<Caregiver>()
                .Property(c => c.IsVerified)
                .HasColumnType("bit");
        }
    }
}