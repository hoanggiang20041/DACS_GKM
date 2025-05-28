using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Chamsoc.Models.Temp;

public partial class ChamsocContext : DbContext
{
    public ChamsocContext()
    {
    }

    public ChamsocContext(DbContextOptions<ChamsocContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<CareJob> CareJobs { get; set; }

    public virtual DbSet<Caregiver> Caregivers { get; set; }

    public virtual DbSet<Complaint> Complaints { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Senior> Seniors { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=Chamsoc;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedName] IS NOT NULL)");

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.Balance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<CareJob>(entity =>
        {
            entity.HasIndex(e => e.CaregiverId, "IX_CareJobs_CaregiverId");

            entity.HasIndex(e => e.SeniorId, "IX_CareJobs_SeniorId");

            entity.HasIndex(e => e.ServiceId, "IX_CareJobs_ServiceId");

            entity.Property(e => e.Deposit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DepositAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PaymentMethod).HasDefaultValue("");
            entity.Property(e => e.Rating).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Review).HasMaxLength(500);
            entity.Property(e => e.SeniorName).HasDefaultValue("");
            entity.Property(e => e.SeniorPhone).HasDefaultValue("");
            entity.Property(e => e.ServiceType).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.TotalBill).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Caregiver).WithMany(p => p.CareJobs).HasForeignKey(d => d.CaregiverId);

            entity.HasOne(d => d.Senior).WithMany(p => p.CareJobs)
                .HasForeignKey(d => d.SeniorId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Service).WithMany(p => p.CareJobs)
                .HasForeignKey(d => d.ServiceId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Caregiver>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Caregivers_UserId");

            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Rating).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Skills).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.Caregivers).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.HasIndex(e => e.CaregiverId, "IX_Complaints_CaregiverId");

            entity.HasIndex(e => e.JobId, "IX_Complaints_JobId");

            entity.HasIndex(e => e.SeniorId, "IX_Complaints_SeniorId");

            entity.HasOne(d => d.Caregiver).WithMany(p => p.Complaints)
                .HasForeignKey(d => d.CaregiverId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Job).WithMany(p => p.Complaints)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Senior).WithMany(p => p.Complaints)
                .HasForeignKey(d => d.SeniorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.CareJobId, "IX_Notifications_CareJobId");

            entity.HasIndex(e => e.JobId, "IX_Notifications_JobId");

            entity.HasIndex(e => e.UserId, "IX_Notifications_UserId");

            entity.HasOne(d => d.CareJob).WithMany(p => p.NotificationCareJobs).HasForeignKey(d => d.CareJobId);

            entity.HasOne(d => d.Job).WithMany(p => p.NotificationJobs).HasForeignKey(d => d.JobId);

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.CaregiverId, "IX_Payments_CaregiverId");

            entity.HasIndex(e => e.JobId, "IX_Payments_JobId");

            entity.HasIndex(e => e.SeniorId, "IX_Payments_SeniorId");

            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Caregiver).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CaregiverId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Job).WithMany(p => p.Payments)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Senior).WithMany(p => p.Payments)
                .HasForeignKey(d => d.SeniorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasIndex(e => e.CaregiverId, "IX_Ratings_CaregiverId");

            entity.HasIndex(e => e.JobId, "IX_Ratings_JobId");

            entity.HasIndex(e => e.SeniorId, "IX_Ratings_SeniorId");

            entity.Property(e => e.Comment).HasMaxLength(500);
            entity.Property(e => e.Review).HasMaxLength(500);

            entity.HasOne(d => d.Caregiver).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.CaregiverId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Job).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.JobId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Senior).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.SeniorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Senior>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_Seniors_UserId");

            entity.Property(e => e.CareNeeds).HasMaxLength(500);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.User).WithMany(p => p.Seniors).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
