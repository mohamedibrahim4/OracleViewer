using Oracle.ManagedDataAccess.Client;
using System.Text;

namespace MyOracleViewer
{
    public class OracleMetadataService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _config;
        private readonly string _schema;

        public OracleMetadataService(IConfiguration config)
        {
            _config = config;
            _connectionString = _config.GetConnectionString("OracleDb");
            _schema = _config["OracleSchema"];
        }

        public async Task<List<DbObjectModel>> GetDbObjectsAsync()
        {
            var tables = _config.GetSection("OracleMetadata:Tables").Get<List<string>>() ?? new();
            var procedures = _config.GetSection("OracleMetadata:Procedures").Get<List<string>>() ?? new();
            var packages = _config.GetSection("OracleMetadata:Packages").Get<List<string>>() ?? new();

            var results = new List<DbObjectModel>();

            foreach (var table in tables)
            {
                var parts = table.Split('.');
                results.Add(new DbObjectModel { Type = "TABLE", Name = parts[1], Schema = parts[0] });
            }

            foreach (var proc in procedures)
            {
                var parts = proc.Split('.');
                results.Add(new DbObjectModel { Type = "PROCEDURE", Name = parts[2], Schema = parts[0], Package = parts[1] });
            }

            foreach (var pkg in packages)
            {
                var parts = pkg.Split('.');
                results.Add(new DbObjectModel { Type = "PACKAGE", Name = parts[1], Schema = parts[0] });
            }

            return results;
        }

        public async Task<string> GetObjectDefinitionAsync(DbObjectModel obj)
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            return obj.Type switch
            {
                "TABLE" => await GetTableDDLAsync(conn, obj.Schema, obj.Name),
                "PACKAGE" => await GetPackageBodyAsync(conn, obj.Schema, obj.Name),
                "PROCEDURE" => await GetProcedureBodyAsync(conn, obj.Schema, obj.Package, obj.Name),
                _ => "نوع غير معروف"
            };
        }

        private async Task<string> GetTableDDLAsync(OracleConnection conn, string schema, string table)
        {
            var query = $"SELECT DBMS_METADATA.GET_DDL('TABLE', '{table}', '{schema}') FROM DUAL";
            using var cmd = new OracleCommand(query, conn);
            var result = await cmd.ExecuteScalarAsync();
            return result?.ToString() ?? "لا يوجد DDL";
        }

        private async Task<string> GetPackageBodyAsync(OracleConnection conn, string schema, string package)
        {
            var query = @"SELECT TEXT FROM ALL_SOURCE 
                      WHERE OWNER = :schema AND NAME = :package AND TYPE = 'PACKAGE BODY' 
                      ORDER BY LINE";
            using var cmd = new OracleCommand(query, conn);
            cmd.Parameters.Add(new OracleParameter("schema", schema));
            cmd.Parameters.Add(new OracleParameter("package", package));

            var sb = new StringBuilder();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sb.Append(reader.GetString(0));
            }
            return sb.ToString();
        }

        private async Task<string> GetProcedureBodyAsync(OracleConnection conn, string schema, string package, string procedure)
        {
            var query = @"SELECT TEXT FROM ALL_SOURCE 
                      WHERE OWNER = :schema AND NAME = :package AND TYPE = 'PACKAGE BODY'
                      AND UPPER(TEXT) LIKE :proc
                      ORDER BY LINE";
            using var cmd = new OracleCommand(query, conn);
            cmd.Parameters.Add(new OracleParameter("schema", schema));
            cmd.Parameters.Add(new OracleParameter("package", package));
            cmd.Parameters.Add(new OracleParameter("proc", $"%PROCEDURE {procedure.ToUpper()}%"));

            var sb = new StringBuilder();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sb.Append(reader.GetString(0));
            }
            return sb.Length > 0 ? sb.ToString() : "لم يتم العثور على الإجراء المخزن";
        }
    }
}
