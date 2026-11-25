namespace Vairas.LearningTracking.Domain;

public sealed class PracticalRecordTopic : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid RecordVersionId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public Guid TopicId { get; set; }
}
