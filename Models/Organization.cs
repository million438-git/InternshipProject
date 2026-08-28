using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("department_id", Name = "idx_organizations_department")]
[Index("organization_type", Name = "idx_organizations_type")]
[Index("name", Name = "uq_organizations_name", IsUnique = true)]
public partial class Organization
{
    [Key]
    public ulong id { get; set; }

    public ulong? department_id { get; set; }

    [StringLength(200)]
    public string name { get; set; } = null!;

    [StringLength(100)]
    public string? short_name { get; set; }

    [Column(TypeName = "text")]
    public string? description { get; set; }

    [Column(TypeName = "enum('CLUB','OFFICE','ASSOCIATION','STUDENT_UNION','DEPARTMENT','FACULTY','OTHER')")]
    public string organization_type { get; set; } = null!;

    [StringLength(255)]
    public string? email { get; set; }

    [StringLength(50)]
    public string? phone { get; set; }

    [StringLength(1000)]
    public string? logo_url { get; set; }

    [Column(TypeName = "enum('PENDING','ACTIVE','SUSPENDED','INACTIVE')")]
    public string status { get; set; } = null!;

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("organization")]
    public virtual ICollection<_event> _events { get; set; } = new List<_event>();

    [ForeignKey("department_id")]
    [InverseProperty("organizations")]
    public virtual Department? department { get; set; }

    [InverseProperty("organization")]
    public virtual ICollection<organization_member> organization_members { get; set; } = new List<organization_member>();
}
