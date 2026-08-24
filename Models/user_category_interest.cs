using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[PrimaryKey("user_id", "category_id")]
[Index("category_id", Name = "idx_user_category_category")]
public partial class user_category_interest
{
    [Key]
    public ulong user_id { get; set; }

    [Key]
    public ulong category_id { get; set; }

    [Column(TypeName = "enum('LOW','MEDIUM','HIGH')")]
    public string interest_level { get; set; } = null!;

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("category_id")]
    [InverseProperty("user_category_interests")]
    public virtual event_category category { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("user_category_interests")]
    public virtual User user { get; set; } = null!;
}
