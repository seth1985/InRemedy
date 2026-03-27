using InRemedy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace InRemedy.Api.Data;

public sealed class InRemedyDbContext(DbContextOptions<InRemedyDbContext> options) : DbContext(options)
{
    public DbSet<Remediation> Remediations => Set<Remediation>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<RemediationResult> RemediationResults => Set<RemediationResult>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<ImportStagingRow> ImportStagingRows => Set<ImportStagingRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Remediation>(entity =>
        {
            entity.HasKey(x => x.RemediationId);
            entity.Property(x => x.RemediationName).HasMaxLength(200);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Platform).HasMaxLength(50);
            entity.HasIndex(x => x.RemediationName);
        });

        modelBuilder.Entity<Device>(entity =>
        {
            entity.HasKey(x => x.DeviceId);
            entity.Property(x => x.DeviceName).HasMaxLength(200);
            entity.Property(x => x.PrimaryUser).HasMaxLength(150);
            entity.Property(x => x.Model).HasMaxLength(150);
            entity.HasIndex(x => x.DeviceName);
        });

        modelBuilder.Entity<RemediationResult>(entity =>
        {
            entity.HasKey(x => x.ResultId);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.OutputCategory).HasMaxLength(100);
            entity.Property(x => x.ScriptVersion).HasMaxLength(50);
            entity.HasIndex(x => new { x.RemediationId, x.DeviceId, x.RunTimestampUtc }).IsUnique();
            entity.HasOne(x => x.Remediation).WithMany(x => x.Results).HasForeignKey(x => x.RemediationId);
            entity.HasOne(x => x.Device).WithMany(x => x.Results).HasForeignKey(x => x.DeviceId);
        });

        modelBuilder.Entity<SavedView>(entity =>
        {
            entity.HasKey(x => x.SavedViewId);
            entity.Property(x => x.OwnerUserId).HasMaxLength(120);
            entity.Property(x => x.PageType).HasMaxLength(50);
            entity.Property(x => x.Name).HasMaxLength(150);
            entity.HasIndex(x => new { x.OwnerUserId, x.PageType, x.IsDefault });
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(x => x.ImportBatchId);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.FileHashSha256).HasMaxLength(64);
            entity.Property(x => x.StoredFilePath).HasMaxLength(500);
            entity.Property(x => x.ImportType).HasMaxLength(80);
            entity.Property(x => x.Status).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(500);
            entity.HasMany(x => x.Errors).WithOne(x => x.ImportBatch).HasForeignKey(x => x.ImportBatchId);
            entity.HasIndex(x => x.StartedUtc);
            entity.HasIndex(x => x.FileHashSha256);
        });

        modelBuilder.Entity<ImportError>(entity =>
        {
            entity.HasKey(x => x.ImportErrorId);
            entity.Property(x => x.ColumnName).HasMaxLength(120);
            entity.Property(x => x.ErrorMessage).HasMaxLength(500);
        });

        modelBuilder.Entity<ImportStagingRow>(entity =>
        {
            entity.HasKey(x => x.ImportStagingRowId);
            entity.Property(x => x.RemediationName).HasMaxLength(200);
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.Platform).HasMaxLength(50);
            entity.Property(x => x.DeviceName).HasMaxLength(200);
            entity.Property(x => x.PrimaryUser).HasMaxLength(150);
            entity.Property(x => x.Manufacturer).HasMaxLength(150);
            entity.Property(x => x.Model).HasMaxLength(150);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.OutputCategory).HasMaxLength(100);
            entity.Property(x => x.ScriptVersion).HasMaxLength(50);
            entity.HasIndex(x => x.ImportBatchId);
            entity.HasIndex(x => new { x.ImportBatchId, x.RowNumber });
        });
    }
}
