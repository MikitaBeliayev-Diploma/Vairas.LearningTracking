namespace Vairas.LearningTracking.Persistance.Migrations;
public static class CanonicalSql
{
    public static string Read(string file)
    {
        using var stream = typeof(CanonicalSql).Assembly.GetManifestResourceStream($"Vairas.LearningTracking.Persistance.Migrations.Sql.{file}") ?? throw new InvalidOperationException("Missing frozen migration SQL.");
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
