using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.TagsActivities;

namespace ClassNotes.API.Services.TagsActivities
{
	public interface ITagsActivitiesService
	{
		// Mostrar todas las etiquetas por paginación
		Task<ResponseDto<PaginationDto<List<TagActivityDto>>>> GetTagsListAsync(string searchTerm = "", int page = 1);

		// Obtener etiqueta por id
		Task<ResponseDto<TagActivityDto>> GetTagByIdAsync(Guid id);

		// Crear una nueva etiqueta
		Task<ResponseDto<TagActivityDto>> CreateTagAsync(TagActivityCreateDto dto);

		// Editar una etiqueta
		Task<ResponseDto<TagActivityDto>> UpdateTagAsync(TagActivityEditDto dto, Guid id);

		// Eliminar una etiqueta
		Task<ResponseDto<List<TagActivityDto>>> DeleteTagAsync(List<Guid> listGuidsTags);

		// Metodo para crear un conjunto de tags predeterminadas
		Task<ResponseDto<List<TagActivityDto>>> CreateDefaultTagsAsync(string userId);
	}
}