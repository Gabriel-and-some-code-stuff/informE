using informE.Contracts.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace informE.Infrastructure.Realtime;

// Canal de saída para os operadores: o servidor empurra telemetria, alertas e
// progresso de tarefa. Diferente do AgentHub, aqui a autenticação é JWT
// (o mesmo token do login) — daí o [Authorize].
//
// Não tem método de entrada: o dashboard consulta por REST e só ESCUTA aqui.
// Quem publica é o SignalRDashboardNotifier via IHubContext.
[Authorize]
public class DashboardHub : Hub<IDashboardClient>
{
}
