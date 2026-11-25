using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface INotificationSourceService { Task<NotificationSourceResponse> ReadAsync(Guid school, Guid assessment, CancellationToken ct); }
