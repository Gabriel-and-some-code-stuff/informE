namespace informE.Application.Models;

// O agente se apresenta com o token de uso único que o admin gerou + os dados
// da máquina coletados localmente.
public record EnrollDeviceRequest(
    string EnrollmentToken,
    string Hostname,
    string IpAddress,
    string MacAddress,
    string Os,
    string OsUser,
    Guid? GroupId
);
