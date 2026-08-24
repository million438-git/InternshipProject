using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[PrimaryKey("user_id", "department_id")]
[Index("department_id", Name = "idx_user_dept_sub_department")]
public partial class user_dept_subscription
{
    [Key]
    public ulong user_id { get; set; }

    [Key]
    public ulong department_id { get; set; }

    [MaxLength(6)]
    public DateTime subscribed_at { get; set; }

    [ForeignKey("department_id")]
    [InverseProperty("user_dept_subscriptions")]
    public virtual Department department { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("user_dept_subscriptions")]
    public virtual User user { get; set; } = null!;
}
