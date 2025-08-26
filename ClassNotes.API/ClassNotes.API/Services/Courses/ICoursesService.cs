using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.Courses;

namespace ClassNotes.API.Services.Courses
{
        public interface ICoursesService
        {
                // Listar todos los cursos 	
                Task<ResponseDto<PaginationDto<List<CourseWithSettingDto>>>> GetCoursesListAsync(
                        string searchTerm = "", int page = 1, int? pageSize = null
                );

                // Listar un curso en especifico
                Task<ResponseDto<CourseWithSettingDto>> GetCourseByIdAsync(Guid id);

                // Crear un curso 
                Task<ResponseDto<CourseWithSettingDto>> CreateAsync(CourseWithSettingCreateDto dto);

                // Editar cursos 
                Task<ResponseDto<CourseDto>> EditAsync(CourseEditDto dto, Guid id);

                Task<ResponseDto<CourseWithSettingDto>> EditUbicationAsync(LocationDto dto, Guid id);

                // Eliminar un curso
                Task<ResponseDto<CourseWithSettingDto>> DeleteAsync(Guid id);
                Task<ResponseDto<List<UnitDto>>> GetCourseUnits(Guid id);
        }
}
