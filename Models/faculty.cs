using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("code", Name = "uq_faculties_code", IsUnique = true)]
[Index("name", Name = "uq_faculties_name", IsUnique = true)]
public partial class Faculty
{
    [Key]
    public ulong id { get; set; }

    [StringLength(200)]
    public string name { get; set; } = null!;

    [StringLength(50)]
    public string? code { get; set; }

    [Column(TypeName = "text")]
    public string? description { get; set; }

    [StringLength(200)]
    public string? dean_name { get; set; }

    [StringLength(255)]
    public string? email { get; set; }

    [StringLength(50)]
    public string? phone { get; set; }

    [Required]
    public bool? is_active { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [InverseProperty("faculty")]
    public virtual ICollection<Department> departments { get; set; } = new List<Department>();
}
