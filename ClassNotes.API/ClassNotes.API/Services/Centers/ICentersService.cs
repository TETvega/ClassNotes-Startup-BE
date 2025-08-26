﻿using ClassNotes.API.Dtos.Centers;
using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.CourseNotes;
using iText.Kernel.Geom;
using Microsoft.AspNetCore.Mvc;

namespace ClassNotes.API.Services.Centers
{
    public interface ICentersService
    {
        Task<ResponseDto<CenterDto>> ArchiveAsync(Guid id);
        Task<ResponseDto<CenterDto>> CreateAsync([FromForm] CenterCreateDto dto, IFormFile image);
        Task<ResponseDto<CenterDto>> DeleteAsync(bool confirmation, Guid id);
        Task<ResponseDto<CenterDto>> EditAsync([FromForm] CenterEditDto dto, Guid id, IFormFile image, bool changedImage);
        Task<ResponseDto<CenterDto>> GetCenterByIdAsync(Guid id);
        Task<ResponseDto<PaginationDto<List<CenterExtendDto>>>> GetCentersListAsync(string searchTerm = "", bool? isArchived = null, int? pageSize = null, int page = 1);
        Task<ResponseDto<CenterDto>> RecoverAsync(Guid id);
        Task<ResponseDto<PaginationDto<List<CenterDto>>>> GetCentersActivesListAsync(int? pageSize = null, int page = 1);
    }
}