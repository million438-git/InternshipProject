using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("name", Name = "uq_roles_name", IsUnique = true)]
public partial class Role
{
    [Key]
    public ulong id { get; set; }

    [StringLength(100)]
    public string name { get; set; } = null!;

    [StringLength(500)]
    public string? description { get; set; }

    [Required]
    public bool? is_system_role { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("role")]
    public virtual ICollection<role_permission> role_permissions { get; set; } = new List<role_permission>();

    [InverseProperty("role")]
    public virtual ICollection<user_role> user_roles { get; set; } = new List<user_role>();
}
