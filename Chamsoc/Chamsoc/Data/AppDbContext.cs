using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Chamsoc.Models;

namespace Chamsoc.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Caregiver> Caregivers { get; set; }
        public DbSet<Senior> Seniors { get; set; }
        public DbSet<CareJob> CareJobs { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Service> Services { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<CareJob>()
                .HasOne(j => j.Senior)
                .WithMany(s => s.CareJobs)
                .HasForeignKey(j => j.SeniorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CareJob>()
                .HasOne(j => j.Caregiver)
                .WithMany(c => c.CareJobs)
                .HasForeignKey(j => j.CaregiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<CareJob>()
                .HasOne(j => j.Service)
                .WithMany(s => s.CareJobs)
                .HasForeignKey(j => j.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Complaint>()
                .HasOne(c => c.Job)
                .WithMany(j => j.Complaints)
                .HasForeignKey(c => c.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Complaint>()
                .HasOne(c => c.Caregiver)
                .WithMany()
                .HasForeignKey(c => c.CaregiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Complaint>()
                .HasOne(c => c.Senior)
                .WithMany()
                .HasForeignKey(c => c.SeniorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Rating>()
                .HasOne(r => r.Job)
                .WithMany()
                .HasForeignKey(r => r.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Rating>()
                .HasOne(r => r.Caregiver)
                .WithMany()
                .HasForeignKey(r => r.CaregiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Rating>()
                .HasOne(r => r.Senior)
                .WithMany()
                .HasForeignKey(r => r.SeniorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notification>()
                .HasOne(n => n.Job)
                .WithMany()
                .HasForeignKey(n => n.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            // Cấu hình cho Payment
            builder.Entity<Payment>()
                .HasOne(p => p.Job)
                .WithMany()
                .HasForeignKey(p => p.JobId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Senior)
                .WithMany()
                .HasForeignKey(p => p.SeniorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Payment>()
                .HasOne(p => p.Caregiver)
                .WithMany()
                .HasForeignKey(p => p.CaregiverId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
} 