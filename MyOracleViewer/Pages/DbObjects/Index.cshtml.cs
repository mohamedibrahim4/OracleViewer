using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace MyOracleViewer.Pages.DbObjects
{
    public class DbObject
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string Schema { get; set; }
        public string Package { get; set; } // Optional for package procedures

    }
    public class IndexModel : PageModel
    {

        private readonly IConfiguration _configuration;
        private readonly OracleMetadataService _OracleMetadataService;

        public IndexModel(IConfiguration configuration, OracleMetadataService oracleMetadataService)
        {
            _configuration = configuration;
            _OracleMetadataService = oracleMetadataService;
        }

        public List<DbObject> AllObjects { get; set; } = new List<DbObject>();
        public List<DbObject> PaginatedObjects { get; set; } = new List<DbObject>();
        public List<Dictionary<string, object>>? TableData { get; set; }

        public string? ObjectDefinition { get; set; }
        public string? Search { get; set; }

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }


        public int CurrentDataPage { get; set; } = 1;
        public int DataPageSize { get; set; } = 20;
        public int TotalDataPages { get; set; }


        public async Task OnGetAsync(string? Type, string? Name, string? Schema, string? Package, string? Search, int? PageNumber, bool? ShowData, int? DataPageNumber)
        {

            this.Search = Search;
            CurrentPage = PageNumber ?? 1;
            CurrentDataPage = DataPageNumber ?? 1;

            string connectionString = _configuration.GetConnectionString("OracleDb");
            var schemas = _configuration.GetSection("OracleSchema").Get<List<string>>();
            //var schemas = _configuration.GetSection("OracleSchema").Get<List<string>>();

            using (var conn = new OracleConnection(connectionString))
            {
                await conn.OpenAsync();

                foreach (var schema in schemas)
                {
                    // Tables
                    string tableQuery = @$"
                    SELECT 'TABLE' AS TYPE, TABLE_NAME AS NAME, OWNER AS SCHEMA, NULL AS PACKAGE
                    FROM ALL_TABLES
                    WHERE OWNER = '{schema}'";

                    // Stored Procedures & Functions
                    string procedureQuery = @$"
                    SELECT OBJECT_TYPE AS TYPE, OBJECT_NAME AS NAME, OWNER AS SCHEMA, NULL AS PACKAGE
                    FROM ALL_OBJECTS
                    WHERE OWNER = '{schema}' AND OBJECT_TYPE IN ('PROCEDURE', 'FUNCTION')";

                    // Package Procedures & Functions
                    string packageQuery = @$"
                    SELECT OBJECT_TYPE AS TYPE, OBJECT_NAME AS NAME, OWNER AS SCHEMA ,NULL AS PACKAGE 
                    FROM ALL_PROCEDURES
                    WHERE OWNER = '{schema}' ";

                    // Package Body
                    string packageBodyQuery = @$"
                    SELECT 'PACKAGE BODY' AS TYPE, OBJECT_NAME AS NAME, OWNER AS SCHEMA, NULL AS PACKAGE
                    FROM ALL_OBJECTS
                    WHERE OWNER = '{schema}' AND OBJECT_TYPE = 'PACKAGE BODY'";

                    var fullQuery = $"{tableQuery} UNION ALL {procedureQuery} UNION ALL {packageQuery} UNION ALL {packageBodyQuery}";

                    using (var cmd = new OracleCommand(fullQuery, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var obj = new DbObject
                            {
                                Type = reader.GetString(0),
                                Name = reader.GetString(1),
                                Schema = reader.GetString(2),
                                Package = reader.IsDBNull(3) ? null : reader.GetString(3)
                            };

                            if (string.IsNullOrEmpty(Search) || obj.Name.Contains(Search, StringComparison.OrdinalIgnoreCase))
                            {
                                AllObjects.Add(obj);
                            }
                        }
                    }
                }

                TotalPages = (int)Math.Ceiling(AllObjects.Count / (double)PageSize);
                PaginatedObjects = AllObjects
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToList();

                // Load Definition if requested
                if (!string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(Name) && !string.IsNullOrEmpty(Schema))
                {
                    string defQuery = "";

                    if (Type == "TABLE")
                    {
                        defQuery = @$"
                        SELECT DBMS_METADATA.GET_DDL('TABLE', '{Name}', '{Schema}') FROM DUAL";
                    }
                    else if (Type == "PACKAGE BODY")
                    {
                        defQuery = @$"
                        SELECT TEXT FROM ALL_SOURCE 
                        WHERE TYPE = 'PACKAGE BODY' AND NAME = '{Name}' AND OWNER = '{Schema}'
                        ORDER BY LINE";
                    }
                    else if (Type == "PROCEDURE" || Type == "FUNCTION")
                    {
                        defQuery = @$"
                        SELECT TEXT FROM ALL_SOURCE 
                        WHERE TYPE = '{Type}' AND NAME = '{Name}' AND OWNER = '{Schema}'
                        ORDER BY LINE";
                    }
                    else if (Type == "PROCEDURE" && !string.IsNullOrEmpty(Package))
                    {
                        defQuery = @$"
                        SELECT TEXT FROM ALL_SOURCE 
                        WHERE TYPE = 'PACKAGE' AND NAME = '{Package}' AND OWNER = '{Schema}'
                        ORDER BY LINE";
                    }

                    if (!string.IsNullOrEmpty(defQuery))
                    {
                        using (var defCmd = new OracleCommand(defQuery, conn))
                        using (var defReader = await defCmd.ExecuteReaderAsync())
                        {
                            var sb = new System.Text.StringBuilder();
                            while (await defReader.ReadAsync())
                            {
                                sb.Append(defReader.GetString(0));
                            }
                            ObjectDefinition = sb.ToString();
                        }
                    }


                    if (ShowData == true && Type == "TABLE")
                    {
                        int offset = (CurrentDataPage - 1) * DataPageSize;

                        // Count total rows
                        string countQuery = $"SELECT COUNT(*) FROM \"{Schema}\".\"{Name}\"";
                        using (var countCmd = new OracleCommand(countQuery, conn))
                        {
                            TotalDataPages = (int)Math.Ceiling(Convert.ToDecimal(await countCmd.ExecuteScalarAsync()) / DataPageSize);
                        }

                        string dataQuery = $@"
        SELECT * FROM (
            SELECT t.*, ROWNUM rnum FROM (
                SELECT * FROM ""{Schema}"".""{Name}""
            ) t
            WHERE ROWNUM <= {offset + DataPageSize}
        )
        WHERE rnum > {offset}";

                        using (var dataCmd = new OracleCommand(dataQuery, conn))
                        using (var dataReader = await dataCmd.ExecuteReaderAsync())
                        {
                            TableData = new List<Dictionary<string, object>>();

                            while (await dataReader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();

                                for (int i = 0; i < dataReader.FieldCount; i++)
                                {
                                    row[dataReader.GetName(i)] = dataReader.IsDBNull(i) ? null : dataReader.GetValue(i);
                                }

                                TableData.Add(row);
                            }
                        }
                    }



                }
            }
        }
    }
}
