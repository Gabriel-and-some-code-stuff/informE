using informE.Application.Interfaces.Repositories;
using informE.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace informE.Infrastructure.Persistence.Repositories;

public class EnrollmentTokenRepository(AppDbContext db) : IEnrollmentTokenRepository
{
    public Task<EnrollmentToken?> GetByTokenAsync(string token, CancellationToken ct = default) =>
        db.EnrollmentTokens.FirstOrDefaultAsync(t => t.Token == token, ct);

    public async Task AddAsync(EnrollmentToken enrollmentToken, CancellationToken ct = default) =>
        await db.EnrollmentTokens.AddAsync(enrollmentToken, ct);
}
