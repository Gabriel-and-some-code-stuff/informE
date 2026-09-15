namespace informE.Desktop.Components.State;

public static class AppSession
{
    public static string Email { get; private set; } = string.Empty;
    public static string Username { get; private set; } = string.Empty;
    public static string Role { get; private set; } = string.Empty;
    public static string AccessToken { get; private set; } = string.Empty;
    public static string RefreshToken { get; private set; } = string.Empty;
    public static DateTimeOffset? RefreshTokenExpiresAt { get; private set; }

    public static bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public static void Set(
        string email,
        string username,
        string role,
        string accessToken,
        string refreshToken,
        DateTimeOffset refreshTokenExpiresAt)
    {
        Email = email;
        Username = username;
        Role = role;
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
    }

    public static void Clear()
    {
        Email = string.Empty;
        Username = string.Empty;
        Role = string.Empty;
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        RefreshTokenExpiresAt = null;
    }
}
