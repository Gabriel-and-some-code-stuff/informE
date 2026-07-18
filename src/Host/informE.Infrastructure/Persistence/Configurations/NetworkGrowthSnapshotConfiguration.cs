using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class NetworkGrowthSnapshotConfiguration : IEntityTypeConfiguration<NetworkGrowthSnapshot>
{
    public void Configure(EntityTypeBuilder<NetworkGrowthSnapshot> b)
    {
        b.ToTable("network_growth_snapshots");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).HasDefaultValueSql("gen_random_uuid()");

        // Uma linha por dia (grão do tenant, não do device).
        b.HasIndex(s => s.Date).IsUnique();
    }
}
