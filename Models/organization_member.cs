using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[PrimaryKey("organization_id", "user_id")]
[Index("user_id", Name = "idx_org_members_user")]
public partial class organization_member
{
    [Key]
    public ulong organization_id { get; set; }

    [Key]
    public ulong user_id { get; set; }

    [Column(TypeName = "enum('MEMBER','OFFICER','SECRETARY','TREASURER','PRESIDENT','ADMIN')")]
    public string membership_role { get; set; } = null!;

    [MaxLength(6)]
    public DateTime joined_at { get; set; }

    [MaxLength(6)]
    public DateTime? left_at { get; set; }

    [Required]
    public bool? is_active { get; set; }

    [ForeignKey("organization_id")]
    [InverseProperty("organization_members")]
    public virtual Organization organization { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("organization_members")]
    public virtual User user { get; set; } = null!;
}
