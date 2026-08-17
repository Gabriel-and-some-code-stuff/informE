namespace informE.Application.Models;

// TaskSucceeded só é preenchido quando TaskCompleted = true (último log da
// MachineTask acabou de fechar) — o caller usa isso pra decidir se notifica
// o dashboard via IDashboardNotifier.TaskProgressAsync.
public record RecordCommandResultResponse(bool TaskCompleted, bool? TaskSucceeded);
