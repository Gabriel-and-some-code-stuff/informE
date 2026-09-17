using informE.Application.Interfaces;
using informE.Application.Interfaces.Repositories;
using informE.Domain.Enums;

namespace informE.Application.UseCases;

// PATCH /users/{id} — "Informações Pessoais" da tela Meu Perfil e a edição de
// conta na tela de Administração de Contas.
//
// NÃO troca papel nem status: promoção é ChangeUserRoleUseCase (só SuperAdmin) e
// ativar/desativar é SetUserActiveUseCase. Separados de propósito — juntar tudo
// num "update genérico" abriria caminho para o usuário mudar o próprio papel num
// campo escondido do corpo da requisição.
public class UpdateUserProfileUseCase(
    IUserRepository userRepository,
    DominioDeEmailPolicy dominioDeEmail,
    IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(
        Guid userId,
        string? novoUsername,
        string? novoEmail,
        Guid solicitadoPorUserId,
        UserRole solicitadoPorRole,
        CancellationToken ct = default)
    {
        // Dono edita o próprio perfil; Admin/SuperAdmin editam o de terceiro.
        var ehDono = userId == solicitadoPorUserId;
        var ehPrivilegiado = solicitadoPorRole is UserRole.Admin or UserRole.SuperAdmin;

        if (!ehDono && !ehPrivilegiado)
            throw new UnauthorizedAccessException("Você só pode editar o seu próprio perfil.");

        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new InvalidOperationException($"Usuário {userId} não encontrado.");

        if (!string.IsNullOrWhiteSpace(novoUsername)
            && !string.Equals(novoUsername, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            // Mesmo furo que o e-mail tinha: `users.username` e UNICO, e sem
            // esta checagem renomear para um nome ja usado estourava 23505 no
            // SaveChanges e virava 500 sem explicacao.
            var nomeOcupado = await userRepository.GetByUsernameAsync(novoUsername, ct);
            if (nomeOcupado is not null)
                throw new InvalidOperationException($"Já existe usuário com o nome {novoUsername}.");

            user.UpdateUsername(novoUsername);
        }

        if (!string.IsNullOrWhiteSpace(novoEmail) && !string.Equals(novoEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            dominioDeEmail.Validar(novoEmail);

            // `users.email` é único: sem esta checagem o SaveChanges estouraria
            // 23505 e viraria um 500 sem explicação.
            var ocupado = await userRepository.GetByEmailAsync(novoEmail, ct);
            if (ocupado is not null)
                throw new InvalidOperationException($"Já existe usuário com o e-mail {novoEmail}.");

            user.UpdateEmail(novoEmail);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
