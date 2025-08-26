﻿using System.ComponentModel.DataAnnotations;

namespace ClassNotes.API.Dtos.Courses
{
	public class CourseEditDto
	{
		// Datos del curso
		// Nombre
		[Display(Name = "nombre")]
		[Required(ErrorMessage = "El {0} es requerido.")]
		[StringLength(50, ErrorMessage = "El {0} debe tener menos de {1} caracteres.")]
		public string Name { get; set; }

		// Sección
		[Display(Name = "sección")]
		[StringLength(4, ErrorMessage = "La {0} debe tener menos de {1} caracteres.")]
		public string Section { get; set; }

		// Hora de inicio de la clase
		[Display(Name = "hora de inicio")]
		[Required(ErrorMessage = "La {0} es requerida.")]
		public TimeSpan StartTime { get; set; }

		// Hora de finalización de la clase
		[Display(Name = "hora de finalización")]
		public TimeSpan? FinishTime { get; set; }

		// Codigo
		[Display(Name = "codigo")]
		[StringLength(15, ErrorMessage = "El {0} debe tener menos de {1} caracteres.")]
		public string Code { get; set; }

		// Activo?
		[Display(Name = "es activo")]
		[Required(ErrorMessage = "El campo {0} es requerido.")]
		public bool IsActive { get; set; }
	}
}