﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using Azure;
using ClosedXML.Excel;
using ClassNotes.API.Constants;
using ClassNotes.API.Database;
using ClassNotes.API.Database.Entities;
using ClassNotes.API.Dtos.Activities;
using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.Courses;
using ClassNotes.API.Dtos.Emails;
using ClassNotes.API.Dtos.Students;
using ClassNotes.API.Services.Audit;
using ClassNotes.API.Services.Emails;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace ClassNotes.API.Services.Students
{
    public class StudentsService : IStudentsService
    {
        private readonly ClassNotesContext _context;
        private readonly IMapper _mapper;
        private readonly IEmailSender email;
        private readonly IAuditService _auditService;
        private readonly int PAGE_SIZE;
        private readonly IEmailsService _emailsService;

        // NOTA :
        // EL correo de Bienvenida Esta DESACTIVADO
        // No trabaja Con hilos

        public StudentsService(ClassNotesContext classNotesContext, IAuditService auditService, IMapper mapper, IConfiguration configuration, IEmailsService emailsService)
        {
            _context = classNotesContext;
            _mapper = mapper;
            _auditService = auditService;
            PAGE_SIZE = configuration.GetValue<int>("PageSize:Students");
            _emailsService = emailsService;

        }

        // Realize optimizaciones de querys 
        public async Task<ResponseDto<PaginationDto<List<StudentDto>>>> GetStudentsListAsync(
            string searchTerm = "",
            int? pageSize = null,
            int page = 1
            )
        {
            /**
             * Si pageSize es -1, se devuelve int.MaxValue
             * -1 significa "obtener todos los elementos", por lo que usamos int.MaxValue 
             *  int.MaxValue es 2,147,483,647, que es el valor máximo que puede tener un int en C#.
             *  Math.Max(1, valor) garantiza que currentPageSize nunca sea menor que 1 excepto el -1 al inicio
             *  si pageSize es nulo toma el valor de PAGE_SIZE
             */
            int currentPageSize = pageSize == -1 ? int.MaxValue : Math.Max(1, pageSize ?? PAGE_SIZE);
            int startIndex = (page - 1) * currentPageSize;


            // Necesitamos obtener el i de quien hace la petición
            var userId = _auditService.GetUserId();

            // Consulta base con filtrado por TeacherId
            var studentEntityQuery = _context.Students
                .Where(x => x.TeacherId == userId);

            // Aplicar búsqueda si hay un término
            if (!string.IsNullOrEmpty(searchTerm))
            {
                // https://www.csharptutorial.net/entity-framework-core-tutorial/ef-core-like/
                // Optimizacion de consultas usando EF directamanete que es de SQL 
                studentEntityQuery = studentEntityQuery.Where(x =>
                    EF.Functions.Like(x.FirstName + " " + x.LastName, $"%{searchTerm}%")
                );
            }

            // Obtener total de elementos antes de la paginación
            var totalStudents = await studentEntityQuery.CountAsync();

            // Obtener datos paginados y mapear a DTOs directamente en la consulta
            // cosas de yputube pero tambien lo podes ver en la docu de mapper 
            // https://automapperdocs.readthedocs.io/en/latest/Dependency-injection.html
            // https://stackoverflow.com/questions/53528967/how-to-use-projectto-with-automapper-8-0-dependency-injection

            var studentsDtos = await studentEntityQuery
                .OrderByDescending(x => x.CreatedDate)
                .Skip(startIndex)
                .Take(currentPageSize)
                .ProjectTo<StudentDto>(_mapper.ConfigurationProvider) // Directamente mapeamos en la misma consulta
                .ToListAsync();

            // Calcular total de páginas
            int totalPages = (int)Math.Ceiling((double)totalStudents / currentPageSize);

            // Retornamos la respuesta con los datos paginados
            return new ResponseDto<PaginationDto<List<StudentDto>>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = new PaginationDto<List<StudentDto>>
                {
                    CurrentPage = page,
                    PageSize = currentPageSize,
                    TotalItems = totalStudents,
                    TotalPages = totalPages,
                    Items = studentsDtos,
                    HasPreviousPage = page > 1, // Indica si hay una página anterior disponible
                    HasNextPage = page < totalPages, // Indica si hay una página siguiente disponible
                }
            };
        }

        // Obtener estudiantes de un curso en especifico
        public async Task<ResponseDto<PaginationDto<List<StudentDto>>>> GetStudentsByCourseAsync(
            Guid courseId,
            string searchTerm = "",
            int? pageSize = null,
            int page = 1
            )
        {
            // Determinar el tamaño de página
            int currentPageSize = pageSize == -1 ? int.MaxValue : Math.Max(1, pageSize ?? PAGE_SIZE);
            int startIndex = (page - 1) * currentPageSize;

            // Obtener el ID del usuario que realiza la petición
            var userId = _auditService.GetUserId();

            // Consulta base para obtener los estudiantes del curso específico
            var studentEntityQuery = _context.StudentsCourses
                .Include(sc => sc.Student) // Incluir el estudiante relacionado
                .Where(sc => sc.CourseId == courseId && sc.Student.TeacherId == userId);

            // Aplicar búsqueda si hay un término
            if (!string.IsNullOrEmpty(searchTerm))
            {
                studentEntityQuery = studentEntityQuery.Where(sc =>
                    EF.Functions.Like(sc.Student.FirstName + " " + sc.Student.LastName, $"%{searchTerm}%") ||
                    EF.Functions.Like(sc.Student.Email, $"%{searchTerm}%")
                );
            }

            // Obtener el total de elementos antes de la paginación
            var totalStudents = await studentEntityQuery.CountAsync();

            // Obtener datos paginados y mapear a DTOs directamente en la consulta
            var studentsDtos = await studentEntityQuery
                .OrderByDescending(sc => sc.Student.CreatedDate)
                .Skip(startIndex)
                .Take(currentPageSize)
                .Select(sc => sc.Student) // Seleccionar solo el estudiante
                .ProjectTo<StudentDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            // Calcular el número total de páginas
            int totalPages = (int)Math.Ceiling((double)totalStudents / currentPageSize);

            // Retornar la respuesta con los datos paginados
            return new ResponseDto<PaginationDto<List<StudentDto>>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = new PaginationDto<List<StudentDto>>
                {
                    CurrentPage = page,
                    PageSize = currentPageSize,
                    TotalItems = totalStudents,
                    TotalPages = totalPages,
                    Items = studentsDtos,
                    HasPreviousPage = page > 1, // Indica si hay una página anterior disponible
                    HasNextPage = page < totalPages, // Indica si hay una página siguiente disponible
                }
            };
        }

        // Obtener estudiante por Id

        public async Task<ResponseDto<StudentDto>> GetStudentByIdAsync(Guid id)
        {
            // Necesitamos obtener el i de quien hace la petición
            var userId = _auditService.GetUserId();

            var studentEntity = await _context.Students.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == userId);


            if (studentEntity == null)
            {
                return new ResponseDto<StudentDto>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = MessagesConstant.STU_RECORD_NOT_FOUND
                };
            }

            var studentDto = _mapper.Map<StudentDto>(studentEntity);

            return new ResponseDto<StudentDto>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = studentDto
            };

        }

        public async Task<ResponseDto<StudentResultDto>> CreateStudentAsync(BulkStudentCreateDto bulkStudentCreateDto)
        {
            // Validación inicial
            var validationResult = ValidateBulkCreateRequest(bulkStudentCreateDto);
            if (validationResult != null)
            {
                return validationResult;
            }


            var userId = _auditService.GetUserId();
            var courseEntity = await _context.Courses
                .AsNoTracking()
                .Include(c => c.Center)
                .FirstOrDefaultAsync(c => c.Id == bulkStudentCreateDto.CourseId && c.Center.TeacherId == userId);

            if (courseEntity == null)
            {
                return new ResponseDto<StudentResultDto>
                {
                    StatusCode = 400,
                    Status = false,
                    Message = "El curso no existe o no está asignado a este docente.",
                    Data = null
                };
            }

            var result = new StudentResultDto
            {
                SuccessfulStudents = new List<StudentDto>(),
                DuplicateStudents = new List<StudentDto>(),
                ModifiedEmailStudents = new List<StudentDto>()
            };


            foreach (var studentDto in bulkStudentCreateDto.Students)
            {
                await ProcessStudentCreation(bulkStudentCreateDto, studentDto, userId, result, courseEntity);
            }

            return BuildFinalResponse(result);
        }

        /// <summary>
        /// Esta funcion valida que se tengan datos para procesar y limita el numero de datos a 50
        /// </summary>
        /// <param name="bulkStudentCreateDto">
        /// La lista cruda de los estudiantes a crear. se encuentran en la prop de bulkStudentCreateDto.Students
        /// </param>
        /// <returns></returns>
        private ResponseDto<StudentResultDto>? ValidateBulkCreateRequest(BulkStudentCreateDto bulkStudentCreateDto)
        {
            if (bulkStudentCreateDto?.Students == null || !bulkStudentCreateDto.Students.Any())
            {
                return new ResponseDto<StudentResultDto>
                {
                    StatusCode = 400,
                    Status = false,
                    Message = MessagesConstant.STU_RECORDS_NOT_FOUND,
                    Data = null
                };
            }

            if (bulkStudentCreateDto.Students.Count() > 50)
            {
                return new ResponseDto<StudentResultDto>
                {
                    StatusCode = 400,
                    Status = false,
                    Message = MessagesConstant.STU_MAX_CREATE_LIMIT,
                    Data = null
                };
            }
            return null;
        }

        private async Task ProcessStudentCreation(
            BulkStudentCreateDto bulkStudentCreateDto,
            StudentCreateDto studentDto,
            string userId,
            StudentResultDto result,
            CourseEntity courseEntity
            )
        {

            var existingStudent = await _context.Students
                .FirstOrDefaultAsync(s => s.Email == studentDto.Email);

            if (existingStudent != null)
            {
                await HandleExistingStudent(bulkStudentCreateDto, studentDto, existingStudent, result, userId, courseEntity);
                return;
            }

            await CreateNewStudent(bulkStudentCreateDto, studentDto, userId, result, courseEntity);
        }

        private async Task HandleExistingStudent(
            BulkStudentCreateDto bulkStudentCreateDto,
            StudentCreateDto studentDto,
            StudentEntity existingStudent,
            StudentResultDto result,
            string userId,
            CourseEntity courseEntity
            )
        {
            // Si ya existe un estudiante con el mismo correo, nombre y apellido
            //no procesamos más este estudiante ya que independiente del modo estricto es un duplicado exacto 
            if (
                existingStudent.FirstName == studentDto.FirstName &&
                existingStudent.LastName == studentDto.LastName)
            {
                var mappedStudent = new StudentDto
                {
                    Id = existingStudent.Id,
                    FirstName = existingStudent.FirstName,
                    LastName = existingStudent.LastName,
                    Email = existingStudent.Email
                };
                result.DuplicateStudents.Add(mappedStudent);
                return;
            }


            // Si estamos en modo estricto
            if (bulkStudentCreateDto.StrictMode)
            {
                //Cualquier coincidencia de email se considera duplicado, sin importar los nombres
                result.DuplicateStudents.Add(_mapper.Map<StudentDto>(existingStudent));
                return;
            }

            //Si el modo estricto está desactivado modificamos el correo
            studentDto.Email = await GenerateUniqueEmail(studentDto.Email);

            // Guardamos el estudiante original en la lista de duplicados antes de modificar
            result.DuplicateStudents.Add(_mapper.Map<StudentDto>(existingStudent));

            // Agregamos el estudiante con email modificado a la lista de modificados
            result.ModifiedEmailStudents.Add(new StudentDto
            {
                Id = existingStudent.Id,
                FirstName = studentDto.FirstName,
                LastName = studentDto.LastName,
                Email = studentDto.Email // Ahora tiene el email único generado
            });

            await CreateNewStudent(bulkStudentCreateDto, studentDto, userId, result, courseEntity);

        }

        /// <summary>
        /// Esta funcion genera un Email valido para almacenar en la Bd metodo partiendo de {base email}+{n}@{dominio}
        /// </summary>
        /// <param name="originalEmail"></param>
        /// <returns></returns>
        private async Task<string> GenerateUniqueEmail(string originalEmail)
        {
            string baseEmail = originalEmail.Split('@')[0];
            string domain = originalEmail.Split('@')[1]; // gamil.com y Outlook en Yahoo da error hacer esto
            int counter = 1;
            string newEmail = originalEmail;

            // hasta encontrar un Email Valido
            while (await _context.Students.AnyAsync(s => s.Email == newEmail))
            {
                newEmail = $"{baseEmail}+{counter}@{domain}";
                counter++;
            }

            return newEmail;
        }

        private async Task CreateNewStudent(
            BulkStudentCreateDto bulkStudentCreateDto,
            StudentCreateDto studentDto,
            string userId,
            StudentResultDto result,
            CourseEntity courseEntity
            )
        {
            var studentEntity = _mapper.Map<StudentEntity>(studentDto);
            studentEntity.TeacherId = userId;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Students.Add(studentEntity);
                await _context.SaveChangesAsync();

                await CreateStudentCourseRelation(studentEntity.Id, bulkStudentCreateDto.CourseId);

                await SendWelcomeEmail(studentEntity, courseEntity);

                await transaction.CommitAsync();

                result.SuccessfulStudents.Add(_mapper.Map<StudentDto>(studentEntity));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw new Exception("Error al crear el estudiante");
            }
        }

        /// <summary>
        /// Funcion que agrega el Estudiante a una clase 
        /// LO MATRICULA EN LA CLASE
        /// </summary>
        /// <param name="studentId"></param>
        /// <param name="courseId"></param>
        /// <returns></returns>
        private async Task CreateStudentCourseRelation(Guid studentId, Guid courseId)
        {
            var studentCourse = new StudentCourseEntity
            {
                StudentId = studentId,
                CourseId = courseId
            };
            _context.StudentsCourses.Add(studentCourse);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Envia el correo de Inscripcion al estudiante 
        /// </summary>
        /// <param name="studentEntity"></param>
        /// <returns></returns>
        private async Task SendWelcomeEmail(
            StudentEntity studentEntity,
            CourseEntity course
            )
        {
            string emailContent = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <h2 style='color: #0056b3;'>🎉 ¡Tu Inscripción Está Completa! 🎉</h2>
                    <h3>Hola <strong>{studentEntity.FirstName}</strong>,</h3>
                    <p>Te informamos que has sido inscrito en el curso:</p>
                    <h3 style='color: #28a745;'>📚 {course.Name} 📚</h3>
                    <p>Mediante tu correo: <strong>{studentEntity.Email}</strong>.</p>

                    <hr>

                    <p>Este es un correo informativo y con ello queremos explicarte cómo funciona <strong>ClassNotes</strong>.</p>
                    <p><strong>ClassNotes</strong> es una plataforma para docentes que ayuda a crear registros de asistencias y calificaciones.</p>
                    <p>Te recomendamos estar pendiente de tus correos, ya que a través de este correo inscrito (<strong>{studentEntity.Email}</strong>) te enviaremos tus solicitudes de asistencia.</p>

                    <p><strong>Revisa siempre los correos, ya que nuestras cuentas oficiales son:</strong></p>
                    <ul>
                        <li><strong>classnotesservices@gmail.com</strong></li>
                        <li><strong>classnotes.services@gmail.com</strong></li>
                    </ul>

                    <hr>

                    <p>📢 <strong>¿Cómo funciona la asistencia?</strong></p>
                    <p>Cuando tu docente cree una asistencia, se generará un código de ingreso único que deberás ingresar en el formulario adjunto.</p>
                    <p>Recuerda ingresar y validar para este correo: <strong>{studentEntity.Email}</strong>.</p>

                    <p>🎉 ¡Bienvenido a ClassNotes! 🎉</p>
                </body>
                </html>";

            await _emailsService.SendEmailAsync(new EmailDto
            {
                To = studentEntity.Email,
                Subject = $"🚀 ¡Te Has Inscrito en el Curso {course.Name} ! 📚 Prepárate para Aprender",
                Content = emailContent,
            });
        }
        private ResponseDto<StudentResultDto> BuildFinalResponse(StudentResultDto result)
        {
            return new ResponseDto<StudentResultDto>
            {
                StatusCode = result.SuccessfulStudents.Any() ? 201 : 400,
                Status = result.SuccessfulStudents.Any(),
                Message = result.SuccessfulStudents.Any()
                    ? MessagesConstant.STU_CREATE_SUCCESS
                    : MessagesConstant.OPERATION_FAILED,
                Data = result
            };
        }
        public async Task<ResponseDto<StudentDto>> UpdateStudentAsync(Guid id, StudentEditDto studentEditDto)
        {
            // Necesitamos obtener el id de quien hace la petición
            var userId = _auditService.GetUserId();

            // Buscar el estudiante por su ID
            var studentEntity = await _context.Students
                .FirstOrDefaultAsync(s => s.Id == id && s.TeacherId == userId); // Solo quien lo crea lo puede editar


            // Si el estudiante no existe, retornamos el mensaje que no existe
            if (studentEntity == null)
            {
                return new ResponseDto<StudentDto>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = MessagesConstant.STU_RECORD_NOT_FOUND,
                    Data = null
                };
            }

            // Verificar si el nuevo correo ya esta registrado con otro estudiante
            if (studentEditDto.Email != studentEntity.Email)
            {
                var emailExists = await _context.Students
                    .AnyAsync(s => s.Email == studentEditDto.Email);

                if (emailExists)
                {
                    return new ResponseDto<StudentDto>
                    {
                        StatusCode = 400,
                        Status = false,
                        Message = MessagesConstant.EMAIL_ALREADY_REGISTERED,
                        Data = null
                    };
                }
            }

            // Actualizar los campos del estudiante
            _mapper.Map(studentEditDto, studentEntity);

            // Guardar los cambios en la base de datos
            await _context.SaveChangesAsync();

            // Mapear la entidad actualizada a un DTO para la respuesta
            var studentDto = _mapper.Map<StudentDto>(studentEntity);

            // Retornar la respuesta con el DTO
            return new ResponseDto<StudentDto>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_UPDATE_SUCCESS,
                Data = studentDto
            };
        }

        //Servicio para obtener el listado de actividades faltantes por revisar de un alumno
        public async Task<ResponseDto<PaginationDto<List<ActivityDto>>>> GetStudentPendingActivitiesAsync(Guid id, int? pageSize = null, int page = 1)
        {
            var userId = _auditService.GetUserId();

            //Se busca la entidad de estudiante...
            var studentEntity = await _context.Students.FirstOrDefaultAsync(c => c.Id == id && c.TeacherId == userId);

            //Si no existe, retornara error...
            if (studentEntity == null)
            {
                return new ResponseDto<PaginationDto<List<ActivityDto>>>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = MessagesConstant.STU_RECORD_NOT_FOUND
                };
            }

            //Paginacion...
            int currentPageSize = Math.Max(1, pageSize ?? PAGE_SIZE);
            int startIndex = (page - 1) * currentPageSize;

            //Busca las actividades no revisadas a las que no se les pasó el tiempo de revisión...
            var pendingActivitiesList = await _context.Activities.Where(x => !x.StudentNotes.Any(u => u.StudentId == id) && x.QualificationDate <= DateTime.UtcNow).ToListAsync();

            var totalactivities = pendingActivitiesList.Count();


            var studentsDtos = pendingActivitiesList
                .OrderByDescending(x => x.CreatedDate)
                .Skip(startIndex)
                .Take(currentPageSize)
                .ToList();

            var activitiesDto = _mapper.Map<List<ActivityDto>>(pendingActivitiesList);

            int totalPages = (int)Math.Ceiling((double)totalactivities / currentPageSize);

            return new ResponseDto<PaginationDto<List<ActivityDto>>>
            {
                StatusCode = 200,
                Status = false,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = new PaginationDto<List<ActivityDto>>
                {
                    CurrentPage = page,
                    PageSize = currentPageSize,
                    TotalItems = totalactivities,
                    TotalPages = totalPages,
                    Items = activitiesDto,
                    HasPreviousPage = page > 1,
                    HasNextPage = page < totalPages,
                }
            };

        }


        public async Task<ResponseDto<List<Guid>>> DeleteStudentsInBatchAsync(List<Guid> studentIds, Guid courseId)
        {
            // Obtener el ID del usuario que realiza la petición
            var userId = _auditService.GetUserId();

            // Filtrar estudiantes que coincidan con los IDs proporcionados y el TeacherId del usuario
            var studentsToDelete = await _context.Students
                .Where(s => studentIds.Contains(s.Id) && s.TeacherId == userId)
                .ToListAsync();

            // Si no se encuentra ningún estudiante, devolver un error
            if (!studentsToDelete.Any())
            {
                return new ResponseDto<List<Guid>>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = MessagesConstant.STU_RECORD_NOT_FOUND,
                };
            }

            // Obtener la cantidad de cursos en los que están inscritos los estudiantes
            var studentCourseRelations = await _context.StudentsCourses
                .Where(sc => studentIds.Contains(sc.StudentId))
                .GroupBy(sc => sc.StudentId)
                .Select(g => new { StudentId = g.Key, CourseCount = g.Count() })
                .ToListAsync();

            // Eliminar los registros en StudentsCourses SOLO del curso específico
            var relatedRecords = await _context.StudentsCourses
                .Where(sc => studentIds.Contains(sc.StudentId) && sc.CourseId == courseId)
                .ToListAsync();

            //Misma funcionalidad para attendances
            var relatedAttRecords = await _context.Attendances
                .Where(sc => studentIds.Contains(sc.StudentId) && sc.CourseId == courseId)
                .ToListAsync();
            //Misma funcionalidad para activityNotes...
            var ActivityRecords = await _context.StudentsActivitiesNotes.Include(x => x.Activity).ThenInclude(x => x.Unit)
                .Where(sc => studentIds.Contains(sc.StudentId))
                .ToListAsync();

            //Se deben separar las studentActivityNotes para no quita las no relacionadas, de estas, solo queremos el id de estudiante...
            var unrelatedActivities = ActivityRecords.Where(x => x.Activity.Unit.CourseId != courseId).Select(x => x.StudentId);
            var relatedActivities = ActivityRecords.Where(x => x.Activity.Unit.CourseId == courseId);


            _context.Attendances.RemoveRange(relatedAttRecords);
            _context.StudentsActivitiesNotes.RemoveRange(relatedActivities);

            //Antes de borrar los StudentCourses, se debe ir por cada uno y borrar las student unit relacionadas...
            foreach (var item in relatedRecords)
            {
                var relatedUnits = _context.StudentsUnits.Where(x => x.StudentCourseId == item.Id);

                _context.StudentsUnits.RemoveRange(relatedUnits);
            }

            _context.StudentsCourses.RemoveRange(relatedRecords);


            // Determinar qué estudiantes pueden eliminarse de la tabla Students
            var studentsToKeep = studentCourseRelations
                .Where(sc => sc.CourseCount > 1) // Están inscritos en más de un curso
                .Select(sc => sc.StudentId)
                .ToHashSet();


            //Se quitaran solo los estudiantes que no tengand actividades no relacionadas, pues estas necesitan el id...
            var studentsToRemoveFromStudents = studentsToDelete
                .Where(s => !studentsToKeep.Contains(s.Id) && !unrelatedActivities.Contains(s.Id))
                .ToList();

            // Eliminar de la tabla Students solo los que no tienen más cursos
            if (studentsToRemoveFromStudents.Any())
            {
                _context.Students.RemoveRange(studentsToRemoveFromStudents);
            }

            await _context.SaveChangesAsync();

            // Retornar respuesta con los IDs eliminados de StudentsCourses y Students
            return new ResponseDto<List<Guid>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_DELETE_SUCCESS,
                Data = studentsToRemoveFromStudents.Select(s => s.Id).ToList()
            };
        }

        public async Task<ResponseDto<List<StudentDto>>> ReadExcelFileAsync(Guid Id, IFormFile file, bool strictMode = true)
        {
            //Se obtiene el id de usuario para poner como teacher id y para validaciones...
            var userId = _auditService.GetUserId();

            var courseEntity = await _context.Courses
             .AsNoTracking()
             .Include(c => c.Center)
             .FirstOrDefaultAsync(c => c.Id == Id && c.Center.TeacherId == userId);

            if (courseEntity == null)
            {
                return new ResponseDto<List<StudentDto>>
                {
                    StatusCode = 400,
                    Status = false,
                    Message = "El curso no existe o no está asignado a este docente.",
                    Data = null
                };
            }

            //Si el archivo es nulo o esta vacio...
            if (file == null || file.Length == 0)
            {
                return new ResponseDto<List<StudentDto>>
                {
                    StatusCode = 405,
                    Status = false,
                    Message = "Archivo vacío, ingrese uno diferente.",
                    Data = null
                };
            }

            //Se usa Patch.GetExtensio porque es especifico para obtener extension de archivos.
            string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            //Con la extension se valida que sea un archivo xlsx, tampoco acepta .slx por se muy viejo...
            if (fileExtension != ".xlsx")
            {

                return new ResponseDto<List<StudentDto>>
                {
                    StatusCode = 400,
                    Status = false,
                    Message = $"Solo se aceptan archivos .xlsx.",
                    Data = null
                };
            }

            //Listado de titulos validos para columnas...
            var allowedFirstNameColumns = new HashSet<string> { "primer nombre", "nombre", "nombres", "name", "names", "first name" };
            var allowedLastNameColumns = new HashSet<string> { "apellido", "apellidos", "last name", "surname", "surnames" };
            var allowedEmailColumns = new HashSet<string> { "email", "correo", "correo electrónico", "e-mail", "mail" };


            //Aqui se guardaran los studentCreateDto, se usan para aprovechar sus validaciones individuales de propiedades...
            var dataList = new List<StudentCreateDto>();

            //Indica que s creara un nuevo archivo en memoria
            using (var memoryStream = new MemoryStream())
            {
                //Esto pasa los datos de "file" dentro del espacio en memoria que hicimos...
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                //Esto es para indicar que se trabajara con un archivo
                using (var workbook = new XLWorkbook(memoryStream))
                {
                    //Busca la primera pagina del excel
                    var worksheet = workbook.Worksheets.FirstOrDefault();
                    //Validacion de que si tenga, posiblemente innecesaria pero quien sabe...
                    if (worksheet == null)
                    {
                        return new ResponseDto<List<StudentDto>>
                        {
                            StatusCode = 405,
                            Status = false,
                            Message = "El archivo está vacío.",
                            Data = null
                        };
                    }

                    //Se busca la primera fila que no este vacía, si es uno se empieza a contar desde la primera fila...
                    var firstRow = worksheet.FirstRowUsed()?.RowNumber() ?? 1;
                    //Lo mismo con la misma fila usada dentro del archivo, si es 0 se considera que no habia ningún alumno en la lista...
                    var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                    //Para tener un listado de celdas editadas de la primera fila (La de los titulos)...
                    var EditedCells = worksheet.FirstRowUsed()?.CellsUsed();

                    //Contador de filas si agregadas...
                    int u = 0;

                    //Se buscan las celdas cuyo contenido encaje con los titulos válidos, una para cada propiedad...
                    var FirstNameCell = EditedCells.FirstOrDefault(x => allowedFirstNameColumns.Contains(x.Value.ToString().Trim().ToLower()));
                    var LastNameCell = EditedCells.FirstOrDefault(x => allowedLastNameColumns.Contains(x.Value.ToString().Trim().ToLower()));
                    var EMailCell = EditedCells.FirstOrDefault(x => allowedEmailColumns.Contains(x.Value.ToString().Trim().ToLower()));

                    //Si al menos una celda no corresponde se lanza el error...
                    if (FirstNameCell == null || LastNameCell == null || EMailCell == null)
                    {
                        return new ResponseDto<List<StudentDto>>
                        {
                            StatusCode = 405,
                            Status = false,
                            Message = "Estructura de columnas incorrecta, asegurese que su documento contenga solo 1 fila de titulos, y que conste unicamente de 3 columnas tituladas 'Nombre', 'Apellido' y 'Correo'.",
                            Data = null
                        };
                    }

                    //El bucle termina con la ultima fila de alumnos, y empieza una fila despues de la primera no vacia en el archivo, asumiendo que la primera son titulos de columna...
                    for (int i = firstRow + 1; i <= lastRow; i++)
                    {
                        //row cambia en cada iteracion...
                        var row = worksheet.Row(i);

                        //De las celdas con el titulo correspondiente, se toma su columna, y la fila se toma de i, para obbtener el valor de las propiedades en esta iteracion...
                        var firstName = FirstNameCell.WorksheetColumn().Cell(i).Value.ToString().Trim();
                        var lastName = LastNameCell.WorksheetColumn().Cell(i).Value.ToString().Trim();
                        var eMail = EMailCell.WorksheetColumn().Cell(i).Value.ToString().Trim();

                        //Si todas las propiedades son nulas, se salta la fila...
                        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && string.IsNullOrWhiteSpace(eMail))
                        {
                            continue;
                        }
                        else
                        {   //Si solo una propiedad es nula, dara error para evitar estudiantes incompletos...
                            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(eMail))
                            {
                                return new ResponseDto<List<StudentDto>>
                                {
                                    StatusCode = 405,
                                    Status = false,
                                    Message = $"La fila número {row.RowNumber()} contiene propiedades vacías, asegúrese de llenar toda la información.",
                                    Data = null
                                };
                            }
                        }

                        //Si el contador es igual o mayor a 50, se repitio 51 veces, por lo que se agregaron 51 estudiantes, dando un error...
                        if (u == 50)
                        {
                            return new ResponseDto<List<StudentDto>>
                            {
                                StatusCode = 405,
                                Status = false,
                                Message = "Se permiten máximo 50 filas de de estudiantes a la vez.",
                                Data = null
                            };
                        }

                        //Aumento para el contador
                        u++;

                        //Se crea el StudentCreateDto y se agrega a la lista, asi, si hay un error en una iteracion la lista no sera agregada..
                        var createdStudentDto = new StudentCreateDto
                        {
                            FirstName = firstName,
                            LastName = lastName,
                            Email = eMail
                        };

                        dataList.Add(createdStudentDto);
                    }
                }
            }

            //Se pasa de create dto a entity
            var studentEntities = _mapper.Map<List<StudentEntity>>(dataList);


            //Por cada entidad, se valida email y se agrega..
            foreach (var item in studentEntities)
            {
                item.TeacherId = userId;

                //si no se envio strictMode como activo, usara generateUniqueEmail..
                if (!strictMode)
                {
                    item.Email = await GenerateUniqueEmail(item.Email);
                }
                else
                {
                    //Si esta activo, se validara normalmente...
                    var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Email == item.Email && s.TeacherId == userId);
                    if (existingStudent != null)
                    {
                        return new ResponseDto<List<StudentDto>>
                        {
                            StatusCode = 405,
                            Status = false,
                            Message = $"El correo '{item.Email}' de el estudiante '{item.FirstName + " " + item.LastName}' ya esta siendo utilizado por '{existingStudent.FirstName + " " + existingStudent.LastName}'.",
                            Data = null
                        };
                    }
                }

                //Se guarda el estudentEntity de esta iteracion, asegurando que generateUniqueEmail lo tome en cuenta posteriormente..
                _context.Students.Add(item);
                await _context.SaveChangesAsync();

                var studentCourse = new StudentCourseEntity
                {
                    StudentId = item.Id,
                    CourseId = Id
                };

                _context.StudentsCourses.Add(studentCourse);
                await _context.SaveChangesAsync();

                Console.WriteLine(studentCourse.Id);

            }
            ;


            var studentDtos = _mapper.Map<List<StudentDto>>(studentEntities);

            return new ResponseDto<List<StudentDto>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = studentDtos
            };


        }

        public async Task<ResponseDto<PaginationDto<List<StudentPendingDto>>>> GetAllStudentsPendingActivitiesAsync(Guid id,
            string searchTerm = "",
            int? pageSize = null,
            int page = 1,
            string StudentType = "ALL", //Filtro: ACTIVE, INACTIVE, ALL
            string ActivityType = "ALL"//Filtro: PENDIENTES, DONE, ALL
            )
        {
            int currentPageSize = pageSize == -1 ? int.MaxValue : Math.Min(50, Math.Max(1, pageSize ?? PAGE_SIZE)); //Tiene Math.Min para no pasarse de 50...
            int startIndex = (page - 1) * currentPageSize;

            var userId = _auditService.GetUserId();

            //Primero se crea, luego se pobla con datos para evitar error...
            IQueryable<StudentCourseEntity> studentEntityQuery;

            switch (StudentType)
            {
                //Si es inactivo, se buscan los studentCourse inactivos..
                case "INACTIVE":
                    studentEntityQuery = _context.StudentsCourses.Include(x => x.Student).Where(x => x.CourseId == id && x.CreatedBy == userId && !x.IsActive);
                    break;
                case "ACTIVE": //Si es ACTIVE, se buscan los activos...
                    studentEntityQuery = _context.StudentsCourses.Include(x => x.Student).Where(x => x.CourseId == id && x.CreatedBy == userId && x.IsActive);
                    break;
                default://De no ser esas dos palabras, los busca todos, asi evita errores por escribir mal algo...
                    studentEntityQuery = _context.StudentsCourses.Include(x => x.Student).Where(x => x.CourseId == id && x.CreatedBy == userId);
                    break;
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {

                studentEntityQuery = studentEntityQuery.Where(x =>
                    EF.Functions.Like(x.Student.FirstName, $"%{searchTerm}%") || //Para buscar el primer nombre O correo...
                    EF.Functions.Like(x.Student.Email, $"%{searchTerm}%")
                );
            }

            //Se obtienen las actividades que ya pasaron junto a sus studentNotes para verificar que actividades no hicieron....
            var Activities = await _context.Activities.Include(x => x.StudentNotes).Where(x => x.Unit.CourseId == id && x.QualificationDate <= DateTime.UtcNow).ToListAsync();

            //En esta lista se ingresaran las dto que se retornaran...
            List<StudentPendingDto> studentPendingList = [];

            //Por cada estudiante en el curso, se busca cuantas actividades no hizo y se crea el dto...
            foreach (var studentCourse in studentEntityQuery)
            {
                //De las actividades, se busca cualquiera que no este dentro de los student note del estudiante..
                var activityCount = Activities.Where(x => !x.StudentNotes.Any(u => u.StudentId == studentCourse.StudentId));

                var studentDto = new StudentPendingDto
                {
                    StudentId = studentCourse.StudentId,
                    FirstName = studentCourse.Student.FirstName,
                    LastName = studentCourse.Student.LastName,
                    IsActive = studentCourse.IsActive,
                    EMail = studentCourse.Student.Email,
                    PendingActivities = activityCount.Count()
                };

                //Si no esta activo el estudiante, no tiene pendientes...
                if (!studentDto.IsActive) { studentDto.PendingActivities = 0; }

                studentPendingList.Add(studentDto);
            }

            switch (ActivityType)
            {
                //Luego se aplica el filtro de si tiene actividades pendientes o no...
                case "DONE"://DONE si no tiene ninguna pendiente...
                    studentPendingList = studentPendingList.Where(x => x.PendingActivities == 0).ToList();
                    break;
                case "PENDIENTES"://Lo opuesto a DONE
                    studentPendingList = studentPendingList.Where(x => x.PendingActivities > 0).ToList();
                    break;
                default://Para evitar errores, cualquier otro termino dara igual a todos, por lo que no se filtra...
                    break;
            }

            var studentsDtos = studentPendingList
                .OrderByDescending(x => x.FirstName)//Se ordena en base al primer nombre...
                .Skip(startIndex)
                .Take(currentPageSize)
                .ToList();

            var totalStudents = studentPendingList.Count();//Conteo luego de pasar los filtros...

            // Calcular total de páginas
            int totalPages = (int)Math.Ceiling((double)totalStudents / currentPageSize);

            return new ResponseDto<PaginationDto<List<StudentPendingDto>>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.STU_RECORD_FOUND,
                Data = new PaginationDto<List<StudentPendingDto>>
                {
                    CurrentPage = page,
                    PageSize = currentPageSize,
                    TotalItems = totalStudents,
                    TotalPages = totalPages,
                    Items = studentsDtos,
                    HasPreviousPage = page > 1,
                    HasNextPage = page < totalPages,
                }
            };
        }

        public async Task<ResponseDto<List<PendingClassesDto>>> GetPendingActivitiesClasesListAsync(
            Guid id,
            int? top = null)
        {
            var userId = _auditService.GetUserId();

            var studentExists = await _context.Students
                .AsNoTracking()
                .AnyAsync(s => s.Id == id && s.Courses.Any(sc => sc.IsActive));

            if (!studentExists)
            {
                return new ResponseDto<List<PendingClassesDto>>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = MessagesConstant.STU_RECORD_NOT_FOUND
                };
            }
            // Verificar que el usuario autenticado sea docente del estudiante
            var isTeacherOfStudent = await _context.Students
                .AsNoTracking()
                .AnyAsync(s => s.Id == id && s.TeacherId == userId);

            if (!isTeacherOfStudent)
            {
                return new ResponseDto<List<PendingClassesDto>>
                {
                    StatusCode = 403,
                    Status = false,
                    Message = MessagesConstant.RECORD_NOT_FOUND
                };
            }

            var pendingClasses = await (
                    from sc in _context.StudentsCourses  //Traeme todas las relaciones estudiante - curso
                                                         //Uní cada actividad a que tenga una unidad cuya propiedad CourseId sea igual al CourseId del curso al que está inscrito el estudiante sc
                    join a in _context.Activities on sc.CourseId equals a.Unit.CourseId
                    where
                         sc.StudentId == id &&
                         sc.IsActive && sc.Course.IsActive &&
                         a.QualificationDate <= DateTime.UtcNow
                    // Solo traé las actividades donde el estudiante no tiene ninguna nota mayor a 0
                    where !a.StudentNotes.Any(sn =>
                          sn.StudentId == id)
                    // todas las actividades que cumplan los filtros en grupos por curso
                    group a by new { sc.CourseId, sc.Course.Name, centerName = sc.Course.Center.Name, centerId = sc.Course.Center.Id, centerAbb = sc.Course.Center.Abbreviation } into g
                    select new PendingClassesDto
                    {
                        CourseId = g.Key.CourseId,
                        CourseName = g.Key.Name,
                        CenterId = g.Key.centerId,
                        CenterName = g.Key.centerName,
                        CenterAbb = g.Key.centerAbb,
                        PendingActivities = g.Count()
                    }


                )
                .OrderByDescending(x => x.PendingActivities)
                .Take(top ?? 10)
                .ToListAsync();
            //var test = _context.Activities.Where(x => !x.StudentNotes.Any(sn => sn.StudentId == id) && x.Unit.Course.Name== "Dibujo");
            //Console.WriteLine(test.Count());
            return new ResponseDto<List<PendingClassesDto>>
            {
                StatusCode = 200,
                Status = true,
                Message = MessagesConstant.RECORDS_FOUND,
                Data = pendingClasses
            };
        }

        public async Task<ResponseDto<StatusModifiStudents>> ChangeIsActiveStudentList(Guid courseId, List<Guid> studentsList)
        {
            var userId = _auditService.GetUserId();

            var course = await _context.Courses
                .Include(c => c.Center)
                .FirstOrDefaultAsync(c => c.Id == courseId && c.Center.TeacherId == userId);

            if (course == null)
            {
                return new ResponseDto<StatusModifiStudents>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = "No este autorizado o Curso no encontrado ",
                    Data = null
                };
            }

            var existingStudentsCount = await _context.Students
                .CountAsync(s => studentsList.Contains(s.Id));

            if (existingStudentsCount != studentsList.Count)
            {
                return new ResponseDto<StatusModifiStudents>
                {
                    StatusCode = 404,
                    Status = false,
                    Message = "Uno o mas estudiantes no existen en la base de datos",
                    Data = null
                };
            }

            var students = await _context.StudentsCourses
                .Where(sc => sc.CourseId == courseId && studentsList.Contains(sc.StudentId))
                .ToListAsync();

            if (students.Count != studentsList.Count)
            {
                return new ResponseDto<StatusModifiStudents>
                {
                    StatusCode = 403,
                    Status = false,
                    Message = "Error: Algunos estudiantes no están inscritos en el curso o no tienes permisos.",
                    Data = null
                };
            }

            // Invertimos el estado uno por uno
            students.ForEach(sc =>
            {
                sc.IsActive = !sc.IsActive;
                sc.UpdatedBy = userId;
                sc.UpdatedDate = DateTime.UtcNow;
            });

            await _context.SaveChangesAsync();

            bool status = students.FirstOrDefault()?.IsActive ?? false;
            return new ResponseDto<StatusModifiStudents>
            {
                StatusCode = 200,
                Status = true,
                Message = $"{students.Count} registros modificados.",
                Data = new StatusModifiStudents
                {
                    StudentsMCount = students.Count,
                    ToIsActive = status
                }
            };
        }
    }
}