using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace HawassaUnifiedCampusEventManagementSystem.Models;

[Index("course_code", Name = "idx_class_course")]
[Index("day_of_week", "start_time", "end_time", Name = "idx_class_day_time")]
[Index("department_id", Name = "idx_class_department")]
public partial class class_schedule
{
    [Key]
    public ulong id { get; set; }

    public ulong department_id { get; set; }

    [StringLength(50)]
    public string course_code { get; set; } = null!;

    [StringLength(255)]
    public string course_name { get; set; } = null!;

    [StringLength(100)]
    public string? section_name { get; set; }

    [StringLength(50)]
    public string? academic_year { get; set; }

    [StringLength(50)]
    public string? semester { get; set; }

    [Column(TypeName = "enum('MONDAY','TUESDAY','WEDNESDAY','THURSDAY','FRIDAY','SATURDAY','SUNDAY')")]
    public string day_of_week { get; set; } = null!;

    [Column(TypeName = "time")]
    public TimeSpan start_time { get; set; }

    [Column(TypeName = "time")]
    public TimeSpan end_time { get; set; }

    [StringLength(200)]
    public string? room_name { get; set; }

    [MaxLength(6)]
    public DateTime created_at { get; set; }

    [MaxLength(6)]
    public DateTime updated_at { get; set; }

    [ForeignKey("department_id")]
    [InverseProperty("class_schedules")]
    public virtual Department department { get; set; } = null!;
}
