﻿using AutoMapper;
using ClassNotes.API.Constants;
using ClassNotes.API.Database;
using ClassNotes.API.Database.Entities;
using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.Courses;
using ClassNotes.API.Dtos.CourseSettings;
using ClassNotes.API.Services.Audit;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using NetTopologySuite;

namespace ClassNotes.API.Services.CoursesSettings
{
	public class CourseSettingsService : ICourseSettingsService
	{
		private readonly ClassNotesContext _context;
		private readonly IMapper _mapper;
		private readonly IAuditService _auditService;
		private readonly int PAGE_SIZE;

		public CourseSettingsService(
			ClassNotesContext context,
			IMapper mapper,
			IAuditService auditService,
			IConfiguration configuration
		)
		{
			_context = context;
			_auditService = auditService;
			_mapper = mapper;
			PAGE_SIZE = configuration.GetValue<int>("PageSize:CourseSettings");
		}

		// Para listar todas las configuraciones de un docente
		public async Task<ResponseDto<PaginationDto<List<CourseSettingDto>>>> GetCourseSettingsListAsync(string searchTerm = "", int page = 1)
		{
			int startIndex = (page - 1) * PAGE_SIZE; // Se calcula el indice inicial para la paginación

			// Este ID se utiliza para filtrar las configuraciones que pertenecen al usuario que realiza la solicitud.
			var userId = _auditService.GetUserId();

			// Se hace una consulta para obtener las configuraciones creadas por el usuario actual.
			var settingsQuery = _context.CoursesSettings
				.Where(c => c.CreatedBy == userId && c.IsOriginal) // Para mostrar unicamente las configuraciones que pertenecen al usuario que hace la petición
				.AsQueryable(); // Ademas se agrega que solo se muestren las originales ya que no nos interesa ver todas las copias que hemos creado

			// buscar por el tipo de puntuación
			if (!string.IsNullOrEmpty(searchTerm))
			{
				settingsQuery = settingsQuery.Where(
					s => s.Name.ToLower().Contains(searchTerm.ToLower())
				);
			}

			int totalItems = await settingsQuery.CountAsync();
			int totalPages = (int)Math.Ceiling((double)totalItems / PAGE_SIZE);

			// aplicar paginacion 

			var settingsEntity = await settingsQuery
				.OrderByDescending(s => s.CreatedDate) //Ordenara por fecha de creación
				.Skip(startIndex)
				.Take(PAGE_SIZE)
				.ToListAsync();

			var settingsDto = _mapper.Map<List<CourseSettingDto>>(settingsEntity);

			return new ResponseDto<PaginationDto<List<CourseSettingDto>>>
			{
				StatusCode = 200,
				Status = true,
				Message = MessagesConstant.CP_RECORD_FOUND,
				Data = new PaginationDto<List<CourseSettingDto>>
				{
					CurrentPage = page,
					PageSize = PAGE_SIZE,
					TotalItems = totalItems,
					TotalPages = totalPages,
					Items = settingsDto,
					HasPreviousPage = page > 1,
					HasNextPage = page < totalPages
				}
			};
		}

		// Para listar una configuración mediante su id
		public async Task<ResponseDto<CourseSettingDto>> GetCourseSettingByIdAsync(Guid id)
		{
			var userId = _auditService.GetUserId(); // Obtener el id de quien hace la petición

			var settingEntity = await _context.CoursesSettings
				.FirstOrDefaultAsync(a => a.Id == id && a.CreatedBy == userId); // Unicamente aprecera la configuración si lo creo quien hace la petición

			if (settingEntity == null)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 404,
					Status = false,
					Message = MessagesConstant.CP_RECORD_NOT_FOUND
				};
			}
			var settingDto = _mapper.Map<CourseSettingDto>(settingEntity);
			return new ResponseDto<CourseSettingDto>
			{
				StatusCode = 200,
				Status = true,
				Message = MessagesConstant.CP_RECORD_FOUND,
				Data = settingDto
			};
		}

		// Para crear una configuración
		public async Task<ResponseDto<CourseSettingDto>> CreateAsync(CourseSettingCreateDto dto)
		{
			var userId = _auditService.GetUserId(); // Id de quien hace la petición


			//Valida que se cree correctamente el tipo de puntaje... esos son los valores que se pueden usar...
			if (dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.GOLD_SCORE &&
				dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.WEIGHTED_SCORE &&
				dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.ARITHMETIC_SCORE)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 405,
					Status = false,
					Message = $"Los tipos de puntaje válidos son: [ {ScoreTypeConstant.GOLD_SCORE} , {ScoreTypeConstant.WEIGHTED_SCORE} , {ScoreTypeConstant.ARITHMETIC_SCORE}  ]"
				};
			}


			// Validar que la fecha de fin de periodo no sea menor a la de inicio
			if (dto.EndDate < dto.StartDate)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 400,
					Status = false,
					Message = MessagesConstant.CP_INVALID_DATES
				};
			}

			// Validar que las puntuaciones sean mayores a 0
			if (dto.MinimumGrade <= 0 || dto.MaximumGrade <= 0)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 400,
					Status = false,
					Message = MessagesConstant.CP_INVALID_GRADES
				};
			}

			// Validar que puntuación maxima no sea menor a la minima
			if (dto.MaximumGrade < dto.MinimumGrade)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 400,
					Status = false,
					Message = MessagesConstant.CP_INVALID_GRADES
				};
			}

			// Verificar si ya existe una configuración igual
			var existingConfiguration = await _context.CoursesSettings
				.FirstOrDefaultAsync(c =>
					c.CreatedBy == userId &&
					c.Name.ToLower() == dto.Name.ToLower() &&
					Math.Round(c.MinimumGrade, 2) == Math.Round(dto.MinimumGrade, 2) &&
					Math.Round(c.MaximumGrade, 2) == Math.Round(dto.MaximumGrade, 2) &&
					c.MinimumAttendanceTime == dto.MinimumAttendanceTime &&
					c.StartDate == dto.StartDate
				);

			if (existingConfiguration != null)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 400,
					Status = false,
					Message = MessagesConstant.CONFIGURATION_ALREADY_EXISTS
				};
			}



			// Pasa las validaciones y se crea la configuración
			var settingEntity = _mapper.Map<CourseSettingEntity>(dto);
			settingEntity.IsOriginal = true; // Aqui se marca la configuración como original

			var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
			var point = geometryFactory.CreatePoint(new Coordinate(dto.GetLocationDto.X, dto.GetLocationDto.Y));

			settingEntity.GeoLocation = point;

			_context.CoursesSettings.Add(settingEntity);
			await _context.SaveChangesAsync();
			var settingDto = _mapper.Map<CourseSettingDto>(settingEntity);
			return new ResponseDto<CourseSettingDto>
			{
				StatusCode = 201,
				Status = true,
				Message = MessagesConstant.CP_CREATE_SUCCESS,
				Data = settingDto
			};
		}

		// Para editar una configuración
		public async Task<ResponseDto<CourseSettingDto>> EditAsync(CourseSettingEditDto dto, Guid id)
		{
			var userId = _auditService.GetUserId(); // Id de quien hace la petición

			// Incluir la relación con settings
			var settingEntity = await _context.CoursesSettings
				.FirstOrDefaultAsync(x => x.Id == id && x.CreatedBy == userId); // Solo el creador puede modificarlo

			if (settingEntity == null)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 404,
					Status = false,
					Message = MessagesConstant.CP_RECORD_NOT_FOUND
				};
			}

			//Valida que se cree correctamente el tipo de puntaje... esos son los valores que se pueden usar...
			if (dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.GOLD_SCORE &&
				dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.WEIGHTED_SCORE &&
				dto.ScoreType.ToUpper().Trim() != ScoreTypeConstant.ARITHMETIC_SCORE)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 405,
					Status = false,
					Message = $"Los tipos de puntaje válidos son: [ {ScoreTypeConstant.GOLD_SCORE} , {ScoreTypeConstant.WEIGHTED_SCORE} , {ScoreTypeConstant.ARITHMETIC_SCORE}  ]"
				};
			}

			_mapper.Map(dto, settingEntity);

			var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
			var point = geometryFactory.CreatePoint(new Coordinate(dto.GetLocationDto.X, dto.GetLocationDto.Y));

			settingEntity.GeoLocation = point;
			_context.CoursesSettings.Update(settingEntity);
			await _context.SaveChangesAsync();

			var settingDto = _mapper.Map<CourseSettingDto>(settingEntity);

			return new ResponseDto<CourseSettingDto>
			{
				StatusCode = 200,
				Status = true,
				Message = MessagesConstant.CP_UPDATE_SUCCESS,
				Data = settingDto
			};
		}

		// Para borrar una configuración
		public async Task<ResponseDto<CourseSettingDto>> DeleteAsync(Guid id)
		{
			// Necesitamos obtener el id de quien hace la petición
			var userId = _auditService.GetUserId();

			// Buscar la configuración por su ID
			var settingEntity = await _context.CoursesSettings
				.FirstOrDefaultAsync(s => s.Id == id && s.CreatedBy == userId); // Solo quien la crea puede borrarla

			// Si la configuración no existe, retornar un error
			if (settingEntity == null)
			{
				return new ResponseDto<CourseSettingDto>
				{
					StatusCode = 404,
					Status = false,
					Message = MessagesConstant.CP_RECORD_NOT_FOUND,
				};
			}

			// // Eliminar registros relacionados en students_courses
			// var relatedRecords = await classNotesContext_.StudentsCourses
			//     .Where(sc => sc.StudentId == id)
			//     .ToListAsync();
			// classNotesContext_.StudentsCourses.RemoveRange(relatedRecords);

			// Eliminar la configuración
			_context.CoursesSettings.Remove(settingEntity);
			await _context.SaveChangesAsync();

			// Retornarnamos una respuesta exitosa
			return new ResponseDto<CourseSettingDto>
			{
				StatusCode = 200,
				Status = true,
				Message = MessagesConstant.CP_DELETE_SUCCESS
			};
		}
	}
}