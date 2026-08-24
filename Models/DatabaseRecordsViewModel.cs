using System;
using System.Collections.Generic;

namespace HawassaUnifiedCampusEventManagementSystem.Models
{
    public class DatabaseRecordsViewModel
    {
        public string ActiveTable { get; set; } = "events";
        public List<DatabaseTableInfo> AvailableTables { get; set; } = new();
        public List<DatabaseColumnMeta> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public int TotalRecords { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages => (int)Math.Ceiling((double)TotalRecords / (PageSize > 0 ? PageSize : 25));
        public string? SearchQuery { get; set; }
    }

    public class DatabaseTableInfo
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Icon { get; set; } = "bi-table";
        public string Description { get; set; } = string.Empty;
        public int RecordCount { get; set; }
    }

    public class DatabaseColumnMeta
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DataType { get; set; } = "string"; // string, number, boolean, datetime, enum, text
        public bool IsPrimaryKey { get; set; }
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }
        public List<string>? EnumOptions { get; set; }
        public string? DefaultValue { get; set; }
    }

    public class DatabaseRecordMutationModel
    {
        public string Table { get; set; } = string.Empty;
        public ulong? Id { get; set; }
        public Dictionary<string, string?> Fields { get; set; } = new();
    }

    public class DatabaseRecordDeleteModel
    {
        public string Table { get; set; } = string.Empty;
        public ulong Id { get; set; }
    }

    public class DatabaseBatchMutationModel
    {
        public string Table { get; set; } = string.Empty;
        public List<DatabaseRecordMutationModel> Insertions { get; set; } = new();
        public List<DatabaseRecordMutationModel> Updates { get; set; } = new();
        public List<ulong> Deletions { get; set; } = new();
    }

    public class DatabaseCrudResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ulong? RecordId { get; set; }
        public int AffectedRows { get; set; }
        public object? Data { get; set; }
    }
}
