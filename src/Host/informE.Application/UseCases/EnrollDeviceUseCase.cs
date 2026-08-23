using System.Security.Cryptography;
using informE.Application.Exceptions;
using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Application.Models;
using informE.Domain.Entities;

namespace informE.Application.UseCases;

// RF01 (auto cadastro) + RF12 (autenticação de agentes via UUID + chave).
// Fluxo: admin gera EnrollmentToken de uso único -> agente chama /enroll com ele
// -> server cria o Device e emite a chave por máquina -> agente guarda com DPAPI.
public class EnrollDeviceUseCase(
    IEnrollmentTokenRepository enrollmentTokenRepository,
    IDeviceRepository deviceRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    private const int TamanhoDaChaveEmBytes = 32;

    public async Task<EnrollDeviceResponse> ExecuteAsync(EnrollDeviceRequest request, CancellationToken ct = default)
    {
        var token = await enrollmentTokenRepository.GetByTokenAsync(request.EnrollmentToken, ct);

        // IsValid() cobre não-usado + não-expirado. Checar aqui dá a exceção certa
        // pra API; Redeem() re-checa e é a garantia real contra reuso.
        if (token is null || !token.IsValid())
            throw new EnrollmentTokenInvalidException();

        // Chave em texto claro sai UMA vez, na resposta. Só o hash é persistido.
        var agentKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoDaChaveEmBytes));

        var device = new Device(
            request.Hostname,
            request.IpAddress,
            request.MacAddress,
            request.Os,
            request.OsUser,
            passwordHasher.Hash(agentKey),
            request.GroupId,
            deviceInfo: null) // inventário de hardware chega depois, em coleta própria (RF02)
        {
            // Igual ao DispatchTaskUseCase: o Id precisa existir antes do
            // SaveChanges porque token.Redeem() referencia o device.
            Id = Guid.NewGuid()
        };

        await deviceRepository.AddAsync(device, ct);

        token.Redeem(device.Id);

        await unitOfWork.SaveChangesAsync(ct);

        return new EnrollDeviceResponse(device.Id, agentKey);
    }
}
