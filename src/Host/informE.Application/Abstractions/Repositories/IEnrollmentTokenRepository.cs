using informE.Domain.Entities;

namespace informE.Application.Abstractions.Repositories;

public interface IEnrollmentTokenRepository
{
    Task<EnrollmentToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(EnrollmentToken enrollmentToken, CancellationToken ct = default);
}
