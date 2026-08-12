using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace informE.Infrastructure.Persistence.Configurations;

public class NetworkGrowthSnapshotConfiguration : IEntityTypeConfiguration<NetworkGrowthSnapshot>
{
    public void Configure(EntityTypeBuilder<NetworkGrowthSnapshot> builder)
    {
        builder.ToTable("network_growth_snapshots");
        builder.HasKey(network => network.Id);
        builder.Property(network => network.Id).HasDefaultValueSql("gen_random_uuid()");

        // Uma linha por dia (grão do tenant, não do device).
        builder.HasIndex(network => network.Date).IsUnique();
    }
}
