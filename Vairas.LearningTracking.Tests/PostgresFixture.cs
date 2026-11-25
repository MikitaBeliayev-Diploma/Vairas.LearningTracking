using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Persistance;
namespace Vairas.LearningTracking.Tests;

internal sealed class PostgresFixture:IAsyncDisposable
{
    private PostgreSqlContainer? container;
    private string admin="",database="";
    private readonly string suffix=Guid.NewGuid().ToString("N");
    public string Owner="",Runtime="",Delivery="";
    public string AdminConnection=>admin;
    public static string Root=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
    public async Task Start()
    {
        admin=Environment.GetEnvironmentVariable("VAIRAS_TEST_ADMIN_CONNECTION")??"";
        if(admin.Length==0){container=new PostgreSqlBuilder("postgres:17-bookworm").Build();await container.StartAsync();admin=container.GetConnectionString();}
        var secret=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        foreach(var role in Roles.Values)await Sql(admin,$"CREATE ROLE {role} LOGIN PASSWORD '{secret}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
        database="learning_"+suffix;await Sql(admin,$"CREATE DATABASE {database} OWNER {Roles["v_learning_tracking_migration"]}");
        var connection=new NpgsqlConnectionStringBuilder(admin){Database=database};
        Owner=new NpgsqlConnectionStringBuilder(connection.ConnectionString){Username=Roles["v_learning_tracking_migration"],Password=secret}.ConnectionString;
        Runtime=new NpgsqlConnectionStringBuilder(connection.ConnectionString){Username=Roles["v_learning_tracking_runtime"],Password=secret}.ConnectionString;
        Delivery=new NpgsqlConnectionStringBuilder(connection.ConnectionString){Username=Roles["v_learning_tracking_delivery"],Password=secret}.ConnectionString;
        await Sql(connection.ConnectionString,$"ALTER SCHEMA public OWNER TO {Roles["v_learning_tracking_migration"]};REVOKE ALL ON SCHEMA public FROM PUBLIC");
        await using(var db=OwnerDb()){await db.Database.MigrateAsync();await db.Database.OpenConnectionAsync();await ((NpgsqlConnection)db.Database.GetDbConnection()).ReloadTypesAsync();}
        foreach(var file in new[]{"canonical-roles.sql","reliability-grants.sql"})
        {
            var sql=string.Join('\n',(await File.ReadAllLinesAsync(Path.Combine(Root,"database",file))).Where(x=>!x.StartsWith('\\')));
            foreach(var (from,to) in Roles)sql=sql.Replace(from,to,StringComparison.Ordinal);
            await Sql(Owner,sql);
        }
        TestContext.Out.WriteLine("PostgreSQL "+await Scalar(Owner,"SHOW server_version"));
    }
    public Dictionary<string,string> Roles=>new(){["v_learning_tracking_migration"]="lt_owner_"+suffix,["v_learning_tracking_runtime"]="lt_runtime_"+suffix,["v_learning_tracking_delivery"]="lt_delivery_"+suffix};
    public LearningDbContext OwnerDb()=>new(new DbContextOptionsBuilder<LearningDbContext>().UseLearning(Owner).Options);
    public ILearningStore Store=>new LearningStore(new PooledDbContextFactory<LearningDbContext>(new DbContextOptionsBuilder<LearningDbContext>().UseLearning(Runtime).Options));
    public static async Task Sql(string connection,string sql){await using var db=new NpgsqlConnection(connection);await db.OpenAsync();await using var command=new NpgsqlCommand(sql,db);await command.ExecuteNonQueryAsync();}
    public static async Task<object?> Scalar(string connection,string sql){await using var db=new NpgsqlConnection(connection);await db.OpenAsync();await using var command=new NpgsqlCommand(sql,db);return await command.ExecuteScalarAsync();}
    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        if(admin.Length>0 && database.Length>0){await Sql(admin,$"DROP DATABASE IF EXISTS {database} WITH(FORCE)");foreach(var role in Roles.Values)await Sql(admin,$"DROP ROLE IF EXISTS {role}");}
        if(container is not null)await container.DisposeAsync();
    }
}
