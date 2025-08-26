﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClassNotes.API.Database.Entities
{
    [Table("activities", Schema = "dbo")]
    public class ActivityEntity : BaseEntity
    {
        [Required]
        [StringLength(50)]
        [Column("name")]
        public string Name { get; set; }

        [StringLength(200)]
        [Column("description")]
        public string? Description { get; set; }

        [Required]
        [Column("is_extra")]
        public bool IsExtra { get; set; }

        [Required]
        [Range(0, 100)]
        [Column("max_score")]
        public float MaxScore { get; set; }

        [Required]
        [Column("tag_activity_id")]
        public Guid TagActivityId { get; set; }
        [ForeignKey(nameof(TagActivityId))]
        public virtual TagActivityEntity TagActivity { get; set; }

        [Required]
        [Column("qualification_date")]
        public DateTime QualificationDate { get; set; }

        [Required]
        [Column("unit_id")]
        public Guid UnitId { get; set; }
        [ForeignKey(nameof(UnitId))]
        public virtual UnitEntity Unit { get; set; }

        public virtual ICollection<StudentActivityNoteEntity> StudentNotes { get; set; }
        public virtual UserEntity CreatedByUser { get; set; }
        public virtual UserEntity UpdatedByUser { get; set; }
    }
}