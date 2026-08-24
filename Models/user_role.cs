using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[PrimaryKey("user_id", "role_id")]
[Index("assigned_by", Name = "idx_user_roles_assigned_by")]
[Index("role_id", Name = "idx_user_roles_role")]
public partial class user_role
{
    [Key]
    public ulong user_id { get; set; }

    [Key]
    public ulong role_id { get; set; }

    public ulong? assigned_by { get; set; }

    [MaxLength(6)]
    public DateTime assigned_at { get; set; }

    [ForeignKey("assigned_by")]
    [InverseProperty("user_roleassigned_byNavigations")]
    public virtual User? assigned_byNavigation { get; set; }

    [ForeignKey("role_id")]
    [InverseProperty("user_roles")]
    public virtual Role role { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("user_roleusers")]
    public virtual User user { get; set; } = null!;
}
