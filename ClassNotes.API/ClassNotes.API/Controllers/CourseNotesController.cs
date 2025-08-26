﻿using ClassNotes.API.Constants;
using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.CourseNotes;
using ClassNotes.API.Services.CourseNotes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClassNotes.API.Controllers
{
	[ApiController]
	[Route("api/course_notes")]
	[Authorize(AuthenticationSchemes = "Bearer")]
	public class CourseNotesController : ControllerBase
	{
		private readonly ICourseNotesService _courseNotesService;

		public CourseNotesController(ICourseNotesService courseNotesService)
		{
			_courseNotesService = courseNotesService;
		}

		// Traer todos 
		[HttpPost("getAllNotes")]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<CourseNoteDto>>> GetAll(
				FilterCourseNotes dto
		)
		{
			var response = await _courseNotesService.GetAllCourseNotesAsync(dto);
			return StatusCode(response.StatusCode, response);
		}

		// Traer por id 
		[HttpGet("{id}")]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<CourseNoteDto>>> Get(Guid id)
		{
			var response = await _courseNotesService.GetCourseNoteByIdAsync(id);
			return StatusCode(response.StatusCode, response);
		}

		// Crear 
		[HttpPost]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<CourseNoteDto>>> Create(CourseNoteCreateDto dto)
		{
			var response = await _courseNotesService.CreateAsync(dto);
			return StatusCode(response.StatusCode, response);
		}

		// Editar
		[HttpPut("{id}")]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<CourseNoteDto>>> Edit(CourseNoteEditDto dto, Guid id)
		{
			var response = await _courseNotesService.EditAsync(dto, id);
			return StatusCode(response.StatusCode, response);
		}

		[HttpPut("notesViews")]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<List<CoursesNotesDtoViews>>>> EditNotesViews(List<Guid> notesList)
		{
			var response = await _courseNotesService.EditListNotesViews(notesList);
			return StatusCode(response.StatusCode, response);
		}

		// Eliminar 
		[HttpDelete("{id}")]
		[Authorize(Roles = $"{RolesConstant.USER}")]
		public async Task<ActionResult<ResponseDto<CourseNoteDto>>> Delete(Guid id)
		{
			var response = await _courseNotesService.DeleteAsync(id);
			return StatusCode(response.StatusCode, response);
		}
	}
}