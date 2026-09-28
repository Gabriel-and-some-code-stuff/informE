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
        return await ReadAsync<LoginResponseDto>(response, ct);
    }

    public async Task<IReadOnlyList<MachineActionDto>> GetActionsAsync(CancellationToken ct = default) =>
        await GetAsync<List<MachineActionDto>>("actions", ct) ?? [];

    public async Task<DeviceListResponseDto> GetDevicesAsync(Guid? groupId = null, string? search = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (groupId is not null) query.Add($"grupoId={groupId}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"busca={Uri.EscapeDataString(search)}");
        var route = query.Count == 0 ? "devices" : $"devices?{string.Join("&", query)}";
        return await GetAsync<DeviceListResponseDto>(route, ct)
            ?? new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []);
    }


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
        var route = query.Count == 0 ? "devices" : $"devices?{string.Join("&", query)}";

        return await GetAsync<DeviceListResponseDto>(route, ct)
            ?? new DeviceListResponseDto(new DeviceSummaryDto(0, 0, 0, 0), []);
    }

    public async Task<DeviceDetailDto> GetDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        await GetAsync<DeviceDetailDto>($"devices/{deviceId}", ct)
            ?? throw new InformEApiException("O equipamento não foi encontrado.");

    public async Task<IReadOnlyList<GroupListItemDto>> GetGroupsAsync(CancellationToken ct = default) =>
        await GetAsync<List<GroupListItemDto>>("groups", ct) ?? [];

    public async Task<AlertsResponseDto> GetAlertsAsync(int dias = 7, int recentes = 8, Guid? groupId = null, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"dias={Math.Clamp(dias, 1, 90)}",
            $"recentes={Math.Clamp(recentes, 1, 200)}"
        };

        if (groupId is not null)
            query.Add($"grupoId={groupId}");

        return await GetAsync<AlertsResponseDto>($"alerts?{string.Join("&", query)}", ct)
            ?? new AlertsResponseDto(0, new Dictionary<string, int>(), [], []);
    }


    public async Task<IReadOnlyList<UserListItemDto>> GetUsersAsync(string? role = null, bool? active = null, string? search = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(role)) query.Add($"papel={Uri.EscapeDataString(role)}");
        if (active is not null) query.Add($"ativo={active.Value.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"busca={Uri.EscapeDataString(search)}");
        var route = query.Count == 0 ? "users" : $"users?{string.Join("&", query)}";
        return await GetAsync<List<UserListItemDto>>(route, ct) ?? [];
    }

    public async Task<UserListItemDto> GetMeAsync(CancellationToken ct = default) =>
        await GetAsync<UserListItemDto>("users/me", ct)
            ?? throw new InformEApiException("Não foi possível carregar o perfil.");

    public async Task<IReadOnlyList<SessionListItemDto>> GetMySessionsAsync(CancellationToken ct = default) =>
        await GetAsync<List<SessionListItemDto>>("users/me/sessions", ct) ?? [];

    public async Task<CreateUserResponseDto> CreateUserAsync(CreateUserRequestDto request, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Post, "users");
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<CreateUserResponseDto>(response, ct);
    }

    public async Task UpdateUserAsync(Guid id, UpdateUserRequestDto request, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Patch, $"users/{id}");
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task ChangeMyPasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Patch, "users/me/password");
        message.Content = JsonContent.Create(new ChangeOwnPasswordRequestDto(currentPassword, newPassword));
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task ChangeUserRoleAsync(Guid id, string role, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Patch, $"users/{id}/role");
        message.Content = JsonContent.Create(new ChangeRoleRequestDto(role));
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task SetUserActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Patch, $"users/{id}/active");
        message.Content = JsonContent.Create(new SetActiveRequestDto(active));
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task RevokeMySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Delete, $"users/me/sessions/{sessionId}");
        using var response = await http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task<IReadOnlyList<ExecutionListItemDto>> GetExecutionsAsync(int limite = 50, CancellationToken ct = default) =>
        await GetAsync<List<ExecutionListItemDto>?>($"tasks?limite={limite}", ct) ?? [];

    public async Task<DispatchTaskResponseDto> DispatchAsync(DispatchTaskRequestDto request, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Post, "tasks");
        message.Content = JsonContent.Create(request);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<DispatchTaskResponseDto>(response, ct);
    }

    public async Task CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Post, $"tasks/{taskId}/cancel");
        using var response = await http.SendAsync(message, ct);
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
        using var message = CreateAuthorizedRequest(HttpMethod.Get, route);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<T>(response, ct);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string route)
    {
        if (!AppSession.IsAuthenticated)
            throw new InformEApiException("Sua sessão não está autenticada. Entre novamente.");

        var message = new HttpRequestMessage(method, route);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AppSession.AccessToken);
        return message;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        return value ?? throw new InformEApiException("O servidor respondeu sem os dados esperados.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var detail = TryReadProblemDetail(body);

        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "E-mail ou senha inválidos, ou a sessão expirou.",
            System.Net.HttpStatusCode.Forbidden => "Sua conta não tem permissão para realizar esta ação.",
            System.Net.HttpStatusCode.Conflict => detail ?? "A operação não pode ser concluída no estado atual.",
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

public sealed class InformEApiException(string message, int? statusCode = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
}
