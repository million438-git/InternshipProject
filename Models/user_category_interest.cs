using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("category_id", Name = "idx_user_category_category")]
[Index("user_id", Name = "idx_user_category_user")]
[Index("user_id", "category_id", Name = "uq_user_category_interest", IsUnique = true)]
public partial class user_category_interest
{
    [Key]
    public ulong interest_id { get; set; }

    public ulong user_id { get; set; }

    public ulong category_id { get; set; }

    [Column(TypeName = "enum('LOW','MEDIUM','HIGH')")]
    public string interest_level { get; set; } = "MEDIUM";

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [ForeignKey("category_id")]
    [InverseProperty("user_category_interests")]
    public virtual event_category category { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("user_category_interests")]
    public virtual User user { get; set; } = null!;
}

