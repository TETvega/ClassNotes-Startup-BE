using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.ComponentModel;
using NetTopologySuite.Geometries;

namespace ClassNotes.API.Database.Entities
{
    [Table("courses_settings", Schema = "dbo")]
    public class CourseSettingEntity : BaseEntity
    {
        [Required]
        [StringLength(25)]
        [Column("name")]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        [Column("score_type")]
        public string ScoreType { get; set; }

        [Required]
        [Column("start_date")]
        public DateTime StartDate { get; set; } // Fecha de inicio del periodo

        [Column("end_date")]
        public DateTime? EndDate { get; set; } // Fecha de fin de periodo

        [Required]
        [Range(0, 100)]
        [Column("minimum_grade")]
        public float MinimumGrade { get; set; }

        [Required]
        [Range(0, 100)]
        [Column("maximum_grade")]
        public float MaximumGrade { get; set; }

        [Required]
        [Column("minimum_attendance_time")]
        [Range(5, 59)]
        [DefaultValue(10)]
        public int MinimumAttendanceTime { get; set; } // tiempo minimo para realizar la asistencia

        [Column("geolocation")]
        public Point GeoLocation { get; set; } // Sistema de geolocalizacion

        [Required]
        [Column("validate_range_mts")]
        [Range(30, 200)]
        [DefaultValue(50)]
        public int ValidateRangeMeters { get; set; }

        [Required]
        [Column("is_original")]
        public bool IsOriginal { get; set; }

        public virtual CourseEntity Course { get; set; } //Para la relación de uno a uno con cursos
        public virtual ICollection<UserEntity> Teachers { get; set; } = new List<UserEntity>();
        public virtual UserEntity CreatedByUser { get; set; }
        public virtual UserEntity UpdatedByUser { get; set; }
    }
}