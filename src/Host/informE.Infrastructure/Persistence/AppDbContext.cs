using informE.Application.Interfaces;
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<MachineTask> MachineTasks => Set<MachineTask>();
    public DbSet<TaskExecutionLog> TaskExecutionLogs => Set<TaskExecutionLog>();
    public DbSet<Software> Softwares => Set<Software>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<DeviceDailyMetrics> DeviceDailyMetrics => Set<DeviceDailyMetrics>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<NetworkGrowthSnapshot> NetworkGrowthSnapshots => Set<NetworkGrowthSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Sequences dos códigos legíveis (EX-2847, USR-0001). Sequence do Postgres
        // em vez de contador na aplicação: o banco garante unicidade sem race entre
        // requests concorrentes, e o valor é gerado no próprio INSERT.
        modelBuilder.HasSequence<int>("task_code_seq").StartsAt(1000);
        modelBuilder.HasSequence<int>("user_code_seq").StartsAt(1);

        // Aplica todas as classes IEntityTypeConfiguration deste assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    // IUnitOfWork.SaveChangesAsync já é satisfeito pelo DbContext.SaveChangesAsync herdado.
}
