using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using informE.Contracts.Dtos.Api;
using informE.Desktop.Components.State;

namespace informE.Desktop.Services;

public sealed class InformEApiClient(HttpClient http)
{
    public async Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("auth/login", new LoginRequestDto(email, password), ct);
        return await ReadAsync<LoginResponseDto>(response, ct, ContextoDaChamada.Login);
    }

    public async Task<IReadOnlyList<MachineActionDto>> GetActionsAsync(CancellationToken ct = default) =>
        await GetAsync<List<MachineActionDto>>("actions", ct) ?? [];

    public async Task<DeviceListResponseDto> GetDevicesAsync(Guid? groupId = null, CancellationToken ct = default)
    {
        var route = groupId is null ? "devices" : $"devices?grupoId={groupId}";
        return await GetAsync<DeviceListResponseDto>(route, ct)
            ?? new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []);
    }

    // Filtros vao para a query string do servidor, nao para um Where na tela: com
    // 105 maquinas ja da diferenca, e o resumo (big numbers) e calculado pelo
    // servidor sobre os itens FILTRADOS — filtrar no cliente desalinharia os dois.
    public async Task<DeviceListResponseDto> GetDevicesFiltradosAsync(
        Guid? groupId = null,
        string? status = null,
        string? busca = null,
        CancellationToken ct = default)
    {
        var query = new List<string>();

        if (groupId is not null) query.Add($"grupoId={groupId}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(busca)) query.Add($"busca={Uri.EscapeDataString(busca)}");

        var route = query.Count == 0 ? "devices" : $"devices?{string.Join('&', query)}";

        return await GetAsync<DeviceListResponseDto>(route, ct)
            ?? new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []);
    }

    public async Task<DeviceDetailDto?> GetDeviceAsync(Guid id, CancellationToken ct = default) =>
        await GetAsync<DeviceDetailDto>($"devices/{id}", ct);

    // Uma chamada serve o Dashboard inteiro: total, contagem por categoria,
    // historico diario (grafico de barras) e os alertas recentes.
    public async Task<AlertsResponseDto> GetAlertsAsync(
        int dias = 7,
        Guid? grupoId = null,
        CancellationToken ct = default)
    {
        var route = grupoId is null ? $"alerts?dias={dias}" : $"alerts?dias={dias}&grupoId={grupoId}";

        return await GetAsync<AlertsResponseDto>(route, ct)
            ?? new AlertsResponseDto(0, new Dictionary<string, int>(), [], []);
    }

    public async Task<IReadOnlyList<GroupListItemDto>> GetGroupsAsync(CancellationToken ct = default) =>
        await GetAsync<List<GroupListItemDto>>("groups", ct) ?? [];

    public async Task<IReadOnlyList<ExecutionListItemDto>> GetExecutionsAsync(int limite = 50, CancellationToken ct = default) =>
        await GetAsync<List<ExecutionListItemDto>?>($"tasks?limite={limite}", ct) ?? [];

    public async Task<DispatchTaskResponseDto> DispatchAsync(DispatchTaskRequestDto request, CancellationToken ct = default)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Post, "tasks", () => JsonContent.Create(request), ct);
        return await ReadAsync<DispatchTaskResponseDto>(response, ct);
    }

    public async Task CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var response = await SendAuthorizedAsync(
            HttpMethod.Post, $"tasks/{taskId}/cancel", content: null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!AppSession.IsAuthenticated)
            return;

        using var message = CreateAuthorizedRequest(HttpMethod.Post, "auth/logout");
        using var response = await http.SendAsync(message, ct);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            await EnsureSuccessAsync(response, ct);
    }

    private async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using var response = await SendAuthorizedAsync(HttpMethod.Get, route, content: null, ct);
        return await ReadAsync<T>(response, ct);
    }

    // TODA chamada autenticada passa por aqui.
    //
    // O access token vale 15 MINUTOS. Sem renovacao automatica o app quebrava no
    // meio do uso: a tela de Execucoes abria, e 15 min depois qualquer acao
    // devolvia 401 pedindo login de novo. Numa gravacao de demonstracao isso
    // acontece no meio da tomada.
    //
    // Recebe uma FACTORY de conteudo, nao um HttpRequestMessage pronto, porque
    // HttpRequestMessage nao pode ser reenviado — o corpo ja foi consumido. Para
    // repetir a chamada e preciso montar a mensagem outra vez.
    private async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpMethod method,
        string route,
        Func<HttpContent>? content,
        CancellationToken ct)
    {
        var response = await EnviarUmaVezAsync(method, route, content, ct);

        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            return response;

        // 401 pode ser token vencido OU sessao revogada de verdade. A unica forma
        // de saber e tentar renovar: se a renovacao falhar, o 401 era real.
        response.Dispose();

        if (!await TentarRenovarAsync(ct))
        {
            // Limpa o estado ANTES de avisar: sem isto o AppSession continuaria
            // com um token morto, o IsAuthenticated seguiria true, e a proxima
            // tela tentaria carregar dado com credencial que nao vale mais --
            // gerando um segundo erro em vez de mandar a pessoa para o login.
            AppSession.Clear();

            throw new InformEApiException("Sua sessão expirou. Entre novamente.", 401);
        }

        return await EnviarUmaVezAsync(method, route, content, ct);
    }

    private async Task<HttpResponseMessage> EnviarUmaVezAsync(
        HttpMethod method,
        string route,
        Func<HttpContent>? content,
        CancellationToken ct)
    {
        using var message = CreateAuthorizedRequest(method, route);

        if (content is not null)
            message.Content = content();

        return await http.SendAsync(message, ct);
    }

    // Uma renovacao por vez: sem o lock, duas telas carregando juntas disparam
    // dois refresh, e o servidor ROTACIONA o token a cada chamada — o segundo
    // chegaria com um token que o primeiro acabou de invalidar, derrubando a
    // sessao justamente por tentar salva-la.
    private static readonly SemaphoreSlim RenovacaoEmCurso = new(1, 1);

    private async Task<bool> TentarRenovarAsync(CancellationToken ct)
    {
        var tokenQueFalhou = AppSession.AccessToken;

        await RenovacaoEmCurso.WaitAsync(ct);
        try
        {
            // Outra chamada renovou enquanto esperavamos o lock: aproveita.
            if (AppSession.AccessToken != tokenQueFalhou)
                return AppSession.IsAuthenticated;

            if (string.IsNullOrWhiteSpace(AppSession.RefreshToken))
                return false;

            using var response = await http.PostAsJsonAsync(
                "auth/refresh", new RefreshRequestDto(AppSession.RefreshToken), ct);

            if (!response.IsSuccessStatusCode)
                return false;

            var renovado = await response.Content.ReadFromJsonAsync<LoginResponseDto>(cancellationToken: ct);

            if (renovado is null)
                return false;

            AppSession.UpdateTokens(
                renovado.AccessToken, renovado.RefreshToken, renovado.RefreshTokenExpiresAt);

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Renovacao e melhor-esforco: se o servidor caiu, quem chamou recebe
            // a mensagem de sessao expirada, que e o que o usuario pode resolver.
            return false;
        }
        finally
        {
            RenovacaoEmCurso.Release();
        }
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string route)
    {
        if (!AppSession.IsAuthenticated)
            throw new InformEApiException("Sua sessão não está autenticada. Entre novamente.");

        var message = new HttpRequestMessage(method, route);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.AccessToken);
        return message;
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct,
        ContextoDaChamada contexto = ContextoDaChamada.Autenticada)
    {
        await EnsureSuccessAsync(response, ct, contexto);
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        return value ?? throw new InformEApiException("O servidor respondeu sem os dados esperados.");
    }

    // O MESMO status HTTP quer dizer coisas diferentes dependendo de onde
    // aconteceu, e a mensagem tem que dizer o que a pessoa pode fazer.
    //
    // 401 na tela de login  = credencial errada -> tentar de novo
    // 401 em qualquer outra = sessao caiu       -> entrar de novo
    //
    // Antes as duas caiam num texto so ("E-mail ou senha inválidos, ou a sessão
    // expirou"), que nao ajuda em nenhum dos dois casos: quem errou a senha fica
    // em duvida se o problema e a sessao, e quem foi deslogado fica conferindo a
    // senha. O contexto vem de QUEM chama, nao de adivinhacao pelo status.
    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct,
        ContextoDaChamada contexto = ContextoDaChamada.Autenticada)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var detail = TryReadProblemDetail(body);

        var message = (contexto, response.StatusCode) switch
        {
            // ── Tela de login ────────────────────────────────────────────────
            (ContextoDaChamada.Login, System.Net.HttpStatusCode.Unauthorized) =>
                "E-mail ou senha incorretos.",

            // O servidor devolve 403 no login quando a conta foi desativada. O
            // detail explica; a mensagem de reserva diz o que fazer.
            (ContextoDaChamada.Login, System.Net.HttpStatusCode.Forbidden) =>
                detail ?? "Esta conta está desativada. Procure o administrador.",

            // 409 no login = limite de 3 dispositivos atingido. O detail traz o
            // numero e a orientacao, entao ele vale mais que qualquer texto fixo.
            (ContextoDaChamada.Login, System.Net.HttpStatusCode.Conflict) =>
                detail ?? "Você já tem 3 dispositivos conectados. Encerre a sessão em um deles para entrar aqui.",

            // ── Chamadas autenticadas ────────────────────────────────────────
            // So chega aqui depois de a renovacao automatica ter falhado (ver
            // SendAuthorizedAsync), entao a sessao morreu de verdade.
            (_, System.Net.HttpStatusCode.Unauthorized) =>
                "Sua sessão expirou. Entre novamente.",

            (_, System.Net.HttpStatusCode.Forbidden) =>
                detail ?? "Sua conta não tem permissão para realizar esta ação.",

            (_, System.Net.HttpStatusCode.Conflict) =>
                detail ?? "A operação não pode ser concluída no estado atual.",

            (_, System.Net.HttpStatusCode.NotFound) =>
                detail ?? "O item não foi encontrado. Ele pode ter sido removido.",

            // 5xx nao e problema de quem esta usando: nao mande conferir dado.
            _ when (int)response.StatusCode >= 500 =>
                "O servidor encontrou um erro. Tente novamente em instantes.",

            _ => detail ?? $"O servidor retornou o erro {(int)response.StatusCode}."
        };

        throw new InformEApiException(message, (int)response.StatusCode);
    }

    private static string? TryReadProblemDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString();

            if (root.TryGetProperty("erro", out var erro) && erro.ValueKind == JsonValueKind.String)
                return erro.GetString();
        }
        catch (JsonException)
        {
        }

        return null;
    }
}

// De onde a chamada partiu. Muda a mensagem, nao o comportamento.
public enum ContextoDaChamada
{
    /// <summary>Tela de login: 401 e credencial errada.</summary>
    Login,

    /// <summary>Qualquer chamada com token: 401 e sessao expirada.</summary>
    Autenticada
}

public sealed class InformEApiException(string message, int? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
