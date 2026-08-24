using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[PrimaryKey("role_id", "permission_id")]
[Index("permission_id", Name = "fk_role_permissions_permission")]
public partial class role_permission
{
    [Key]
    public ulong role_id { get; set; }

    [Key]
    public ulong permission_id { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("permission_id")]
    [InverseProperty("role_permissions")]
    public virtual Permission permission { get; set; } = null!;

    [ForeignKey("role_id")]
    [InverseProperty("role_permissions")]
    public virtual Role role { get; set; } = null!;
}
