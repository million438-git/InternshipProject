using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("module", Name = "idx_permissions_module")]
[Index("name", Name = "uq_permissions_name", IsUnique = true)]
public partial class Permission
{
    [Key]
    public ulong id { get; set; }

    [StringLength(150)]
    public string name { get; set; } = null!;

    [StringLength(500)]
    public string? description { get; set; }

    [StringLength(100)]
    public string? module { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("permission")]
    public virtual ICollection<role_permission> role_permissions { get; set; } = new List<role_permission>();
}
