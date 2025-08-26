using ClassNotes.API.Dtos.CourseSettings;

namespace ClassNotes.API.Dtos.Courses
{
    public class CourseWithSettingCreateDto
    {
        // Propiedades del curso
        public CourseCreateDto Course { get; set; }

        // Propiedades de la configuración del curso
        public CourseSettingCreateDto CourseSetting { get; set; }

        // Lista de unitCreateDto, para que se cree junto con sus unidades...
        public List<UnitCreateDto> Units { get; set; }
    }
}