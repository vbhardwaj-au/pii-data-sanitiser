using MediatR;
using DAL;
using DAL.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using API.Services;

namespace API.Features.Products.RefreshProductSchema;

public record RefreshProductSchemaCommand(Guid ProductId) : IRequest<bool>;

internal sealed class PriorMapRow
{
    public string FullTableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string ObfuscationDataType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool PreserveLength { get; set; }
    public bool IsManuallyConfigured { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? DetectionReasons { get; set; }
}

public class RefreshProductSchemaCommandHandler : IRequestHandler<RefreshProductSchemaCommand, bool>
{
    private readonly PortalDbContext _db;
    private readonly IProductRepository _products;
    private readonly ILogger<RefreshProductSchemaCommandHandler> _logger;
    private readonly IConnectionStringEncryptionService _encryptionService;

    public RefreshProductSchemaCommandHandler(PortalDbContext db, IProductRepository products, ILogger<RefreshProductSchemaCommandHandler> logger, IConnectionStringEncryptionService encryptionService)
    {
        _db = db;
        _products = products;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task<bool> Handle(RefreshProductSchemaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var product = await _products.GetByIdAsync(request.ProductId);
            if (product == null)
            {
                _logger.LogWarning("RefreshSchema: Product not found. ProductId={ProductId}", request.ProductId);
                return false;
            }
            _logger.LogInformation("RefreshSchema: Start for ProductId={ProductId}, Name={Name}, Technology={Tech}", product.Id, product.Name, product.DatabaseTechnology);

            // Get the Source connection string for schema discovery
            var sourceConnection = await _db.ProductConnectionStrings
                .Where(pcs => pcs.ProductId == product.Id && pcs.ConnectionType == "Source" && pcs.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceConnection == null)
            {
                _logger.LogWarning("RefreshSchema: No active Source connection string found for ProductId={ProductId}", request.ProductId);
                _logger.LogWarning("RefreshSchema: Available connections for ProductId={ProductId}:", request.ProductId);
                var allConnections = await _db.ProductConnectionStrings
                    .Where(pcs => pcs.ProductId == product.Id)
                    .ToListAsync(cancellationToken);
                foreach (var conn in allConnections)
                {
                    _logger.LogWarning("  - Type: {Type}, Active: {Active}, HasConnectionString: {HasConn}", 
                        conn.ConnectionType, conn.IsActive, !string.IsNullOrEmpty(conn.ConnectionString));
                }
                return false;
            }
            
            _logger.LogInformation("RefreshSchema: Found Source connection for ProductId={ProductId}, ConnectionType={Type}", 
                request.ProductId, sourceConnection.ConnectionType);

            // Only SQL Server for now. Extend later for PostgreSQL etc.
            // Fetch tables and columns from the target DB
            var tables = new List<(string Schema, string Table)>();
            var columnsByTable = new Dictionary<(string Schema, string Table), List<(string Column, string Type, int? MaxLen, bool IsNullable, int Ordinal)>>();

        // Decrypt the connection string before using it
        _logger.LogInformation("RefreshSchema: Using connection string from Source connection");
        _logger.LogInformation("RefreshSchema: Raw encrypted connection string length={Length}, IsEmpty={IsEmpty}", 
            sourceConnection.ConnectionString?.Length ?? 0,
            string.IsNullOrEmpty(sourceConnection.ConnectionString));
        
        if (string.IsNullOrEmpty(sourceConnection.ConnectionString))
        {
            _logger.LogError("RefreshSchema: Source connection string is empty or null!");
            return false;
        }
        
        // Decrypt the connection string
        string decryptedConnectionString;
        try
        {
            decryptedConnectionString = _encryptionService.DecryptConnectionString(sourceConnection.ConnectionString);
            _logger.LogInformation("RefreshSchema: Successfully decrypted connection string");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshSchema: Failed to decrypt connection string");
            return false;
        }
        
        _logger.LogInformation("RefreshSchema: Connection details - Server={Server}, Database={Database}", 
            new SqlConnectionStringBuilder(decryptedConnectionString).DataSource,
            new SqlConnectionStringBuilder(decryptedConnectionString).InitialCatalog);
            
        var csb = new SqlConnectionStringBuilder(decryptedConnectionString)
        {
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };
        
        _logger.LogInformation("RefreshSchema: Attempting to connect to database...");
        await using (var conn = new SqlConnection(csb.ToString()))
        {
            try
            {
                await conn.OpenAsync(cancellationToken);
                var parsed = new SqlConnectionStringBuilder(csb.ToString());
                _logger.LogInformation("RefreshSchema: Successfully connected to server={Server}, db={Db}", parsed.DataSource, parsed.InitialCatalog);

                // Get tables
                var getTablesCmd = new SqlCommand(@"SELECT TABLE_SCHEMA, TABLE_NAME
                                               FROM INFORMATION_SCHEMA.TABLES
                                               WHERE TABLE_TYPE='BASE TABLE'
                                               ORDER BY TABLE_SCHEMA, TABLE_NAME", conn);
                await using (var reader = await getTablesCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var schema = reader.GetString(0);
                        var table = reader.GetString(1);
                        tables.Add((schema, table));
                    }
                }
                _logger.LogInformation("RefreshSchema: Discovered {TableCount} tables", tables.Count);

                // Get columns
                var getColsCmd = new SqlCommand(@"SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE, ORDINAL_POSITION
                                             FROM INFORMATION_SCHEMA.COLUMNS
                                             ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION", conn);
                await using (var r2 = await getColsCmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await r2.ReadAsync(cancellationToken))
                    {
                        var schema = r2.GetString(0);
                        var table = r2.GetString(1);
                        var column = r2.GetString(2);
                        var type = r2.GetString(3);
                        int? maxLen = r2.IsDBNull(4) ? null : r2.GetInt32(4);
                        var isNullable = string.Equals(r2.GetString(5), "YES", StringComparison.OrdinalIgnoreCase);
                        var ordinal = r2.GetInt32(6);

                        var key = (schema, table);
                        if (!columnsByTable.TryGetValue(key, out var list))
                        {
                            list = new List<(string, string, int?, bool, int)>();
                            columnsByTable[key] = list;
                        }
                        list.Add((column, type, maxLen, isNullable, ordinal));
                    }
                }
                var colCount = columnsByTable.Sum(kv => kv.Value.Count);
                _logger.LogInformation("RefreshSchema: Discovered {ColumnCount} columns", colCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RefreshSchema: Failed to connect or read INFORMATION_SCHEMA for ProductId={ProductId}. Error: {Error}", 
                    request.ProductId, ex.Message);
                _logger.LogError("RefreshSchema: Connection string details - Server={Server}, Database={Database}", 
                    csb.DataSource, csb.InitialCatalog);
                return false;
            }
        }

        // Capture existing mappings so we can reattach them to new column IDs after refresh
        var priorMappings = await (from m in _db.ColumnObfuscationMappings.AsNoTracking()
                                   join tc in _db.TableColumns.AsNoTracking() on m.TableColumnId equals tc.Id
                                   join ds in _db.DatabaseSchemas.AsNoTracking() on tc.DatabaseSchemaId equals ds.Id
                                   where m.ProductId == product.Id
                                   select new PriorMapRow
                                   {
                                       FullTableName = ds.FullTableName!,
                                       ColumnName = tc.ColumnName!,
                                       ObfuscationDataType = m.ObfuscationDataType!,
                                       IsEnabled = m.IsEnabled,
                                       PreserveLength = m.PreserveLength,
                                       IsManuallyConfigured = m.IsManuallyConfigured,
                                       ConfidenceScore = m.ConfidenceScore,
                                       DetectionReasons = m.DetectionReasons
                                   }).ToListAsync(cancellationToken);
        // Use default tuple equality (case-sensitive) to avoid custom comparer mismatch
        var priorMapDict = priorMappings
            .GroupBy<PriorMapRow, (string FullTableName, string ColumnName)>(
                p => (p.FullTableName, p.ColumnName))
            .ToDictionary(g => g.Key, g => g.First());

        // Replace local snapshot for this product
        var existingSchemas = await _db.DatabaseSchemas
            .Where(s => s.ProductId == product.Id)
            .ToListAsync(cancellationToken);
        var existingSchemaIds = existingSchemas.Select(s => s.Id).ToHashSet();
        var existingColumns = await _db.TableColumns
            .Where(c => existingSchemaIds.Contains(c.DatabaseSchemaId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // Remove column mappings first to satisfy FK constraints
        var mappingsToRemove = await _db.ColumnObfuscationMappings
            .Where(m => m.ProductId == product.Id && existingColumns.Contains(m.TableColumnId))
            .ToListAsync(cancellationToken);
        _db.ColumnObfuscationMappings.RemoveRange(mappingsToRemove);
        await _db.SaveChangesAsync(cancellationToken);

        // Then drop columns and schemas
        var columnEntities = await _db.TableColumns
            .Where(c => existingSchemaIds.Contains(c.DatabaseSchemaId))
            .ToListAsync(cancellationToken);
        _db.TableColumns.RemoveRange(columnEntities);
        _db.DatabaseSchemas.RemoveRange(existingSchemas);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("RefreshSchema: Removed {MappingCount} mappings, {ColumnCount} columns, {SchemaCount} schemas",
            mappingsToRemove.Count, columnEntities.Count, existingSchemas.Count);

        // Insert fresh
        foreach (var (schema, table) in tables)
        {
            var ds = new Contracts.Models.DatabaseSchema
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                SchemaName = schema,
                TableName = table,
                FullTableName = $"{schema}.{table}",
                PrimaryKeyColumns = null,
                RowCount = 0,
                IsAnalyzed = true,
                AnalyzedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.DatabaseSchemas.Add(ds);

            if (columnsByTable.TryGetValue((schema, table), out var cols))
            {
                foreach (var (column, type, maxLen, isNullable, ordinal) in cols)
                {
                    var newCol = new Contracts.Models.TableColumn
                    {
                        Id = Guid.NewGuid(),
                        DatabaseSchemaId = ds.Id,
                        ColumnName = column,
                        SqlDataType = type,
                        MaxLength = maxLen,
                        IsNullable = isNullable,
                        IsPrimaryKey = false,
                        OrdinalPosition = ordinal,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.TableColumns.Add(newCol);

                    // Reattach prior mapping if we had one for this table.column
                    var key = ($"{schema}.{table}", column);
                    if (priorMapDict.TryGetValue(key, out var pm))
                    {
                        _db.ColumnObfuscationMappings.Add(new Contracts.Models.ColumnObfuscationMapping
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            TableColumnId = newCol.Id,
                            ObfuscationDataType = pm.ObfuscationDataType,
                            IsEnabled = pm.IsEnabled,
                            PreserveLength = pm.PreserveLength,
                            IsManuallyConfigured = pm.IsManuallyConfigured,
                            ConfidenceScore = pm.ConfidenceScore,
                            DetectionReasons = pm.DetectionReasons,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("RefreshSchema: Completed for ProductId={ProductId}. Inserted {SchemaCount} schemas and {ColumnCount} columns", product.Id, tables.Count, columnsByTable.Sum(k => k.Value.Count));
        return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RefreshSchema: Unhandled error for ProductId={ProductId}", request.ProductId);
            return false;
        }
    }
}
