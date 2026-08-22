using informE.Domain.Enums;

namespace informE.Application.Models;

// Botão "+ Novo Usuário" da tela de Administração de Contas. Password vem em
// texto claro do request e é hasheada aqui — nunca persistida nem logada.
public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    UserRole Role
);
