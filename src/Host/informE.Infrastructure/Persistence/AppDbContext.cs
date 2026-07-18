using informE.Application.Abstractions;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence;

// DbContext único do Host. snake_case aplicado via UseSnakeCaseNamingConvention
// no registro do DI (Program.cs), então "PasswordHash" vira "password_hash" etc.
public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceInfo> DeviceInfos => Set<DeviceInfo>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<EnrollmentToken> EnrollmentTokens => Set<EnrollmentToken>();
    public DbSet<MachineTask> MachineTasks => Set<MachineTask>();
    public DbSet<TaskExecutionLog> TaskExecutionLogs => Set<TaskExecutionLog>();
    public DbSet<Software> Softwares => Set<Software>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica todas as classes IEntityTypeConfiguration deste assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // IUnitOfWork.SaveChangesAsync já é satisfeito pelo DbContext.SaveChangesAsync herdado.
}
