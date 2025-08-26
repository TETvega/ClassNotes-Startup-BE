﻿using AutoMapper;
using ClassNotes.API.Database.Entities;
using ClassNotes.API.Dtos.Activities;
using ClassNotes.API.Dtos.Attendances;
using ClassNotes.API.Dtos.Centers;
using ClassNotes.API.Dtos.Common;
using ClassNotes.API.Dtos.CourseNotes;
using ClassNotes.API.Dtos.Courses;
using ClassNotes.API.Dtos.CourseSettings;
using ClassNotes.API.Dtos.DashboardCourses;
using ClassNotes.API.Dtos.Students;
using ClassNotes.API.Dtos.TagsActivities;
using ClassNotes.API.Dtos.Users;
using NetTopologySuite.Geometries;

namespace ClassNotes.API.Helpers.Automapper
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            MapsForActivities();
            MapsForAttendances();
            MapsForCenters();
            MapsForCourses();
            MapsForStudents();
            MapsForCourseNotes();
            MapsForCourseSettings();
            MapsForUsers();
            MapsForTagsActivities();
            MapsForDashboardCourses();
            MapsForActivityNotes();
            MapsForUnitNotes();
            MapsForTotalNotes();
            MapsForUnits();
        }
        private void MapsForActivities()
        {
            // Mapeo para el get by id (ActivityDto)
            CreateMap<ActivityEntity, ActivityDto>()
                .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => new ActivityDto.UnitInfo
                {
                    Id = src.Unit.Id, // El id de la unidad a la que pertenece
                    Number = src.Unit.UnitNumber // El numero de esa unidad
                }))
                .ForMember(dest => dest.Course, opt => opt.MapFrom(src => new ActivityDto.CourseInfo
                {
                    Id = src.Unit.Course.Id, // El id del curso al que pertenece
                    Name = src.Unit.Course.Name // El nombre del curso
                }));

            CreateMap<ActivityCreateDto, ActivityEntity>();
            CreateMap<ActivityEditDto, ActivityEntity>();
        }

        private void MapsForUnits()
        {
            CreateMap<UnitEntity, UnitDto>();
        }

        private void MapsForAttendances()
        {
            CreateMap<AttendanceEntity, AttendanceDto>();
            CreateMap<AttendanceCreateDto, AttendanceEntity>();
        }

        private void MapsForActivityNotes()
        {
            CreateMap<StudentActivityNoteEntity, StudentActivityNoteDto>();
            CreateMap<StudentActivityNoteCreateDto, StudentActivityNoteEntity>();
            //  CreateMap<StudentActivityNoteEditDto, StudentActivityNoteEntity>();
        }

        private void MapsForUnitNotes()
        {
            CreateMap<StudentUnitEntity, StudentUnitNoteDto>();
        }

        private void MapsForTotalNotes()
        {
            CreateMap<StudentCourseEntity, StudentTotalNoteDto>();
        }

        private void MapsForCenters()
        {
            CreateMap<CenterEntity, CenterDto>();
            CreateMap<CenterCreateDto, CenterEntity>()
                .ForMember(dest => dest.IsArchived, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                //.ForMember(dest => dest.Logo, opt => opt.MapFrom(src => src.Logo.Trim()))
                .ForMember(dest => dest.Abbreviation, opt => opt.MapFrom(src => src.Abbreviation.Trim()));

            CreateMap<CenterEditDto, CenterEntity>()
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.Trim()))
                //.ForMember(dest => dest.Logo, opt => opt.MapFrom(src => src.Logo.Trim()))
                .ForMember(dest => dest.Abbreviation, opt => opt.MapFrom(src => src.Abbreviation.Trim()));
        }

        private void MapsForCourses()
        {
            CreateMap<CourseEntity, CourseDto>();
            CreateMap<CourseCreateDto, CourseEntity>();
            CreateMap<CourseEditDto, CourseEntity>();

            // Mapeo con configuración
            CreateMap<CourseEntity, CourseWithSettingDto>()
                .ForMember(dest => dest.Course, opt => opt.MapFrom(src => src)) // Mapeamos el curso
                .ForMember(dest => dest.CourseSetting, opt => opt.MapFrom(src => src.CourseSetting)) // Mapeamos la configuración
                .ForMember(dest => dest.Units, opt => opt.MapFrom(src => src.Units)); // ahora puede devolver sus unidades...

            CreateMap<Point, LocationDto>()
                .ForMember(dest => dest.X, opt => opt.MapFrom(src => src.X))
                .ForMember(dest => dest.Y, opt => opt.MapFrom(src => src.Y));

            CreateMap<CourseSettingEntity, CourseSettingDto>()
                .ForMember(dest => dest.GeoLocation, opt => opt.MapFrom(src => src.GeoLocation));
            CreateMap<LocationDto, Point>()
                 .ConvertUsing(dto => new Point(dto.X, dto.Y) { SRID = 4326 });
        }

        private void MapsForStudents()
        {
            CreateMap<StudentEntity, StudentDto>();
            CreateMap<StudentCreateDto, StudentEntity>();
            CreateMap<StudentEditDto, StudentEntity>();
        }

        private void MapsForCourseNotes()
        {
            CreateMap<CourseNoteEntity, CourseNoteDto>();
            CreateMap<CourseNoteEntity, CoursesNotesDtoViews>();
            CreateMap<CourseNoteCreateDto, CourseNoteEntity>();
            CreateMap<CourseNoteEditDto, CourseNoteEntity>();
        }

        private void MapsForCourseSettings()
        {
            CreateMap<CourseSettingEntity, CourseSettingDto>();
            CreateMap<CourseSettingCreateDto, CourseSettingEntity>();
            CreateMap<CourseSettingEditDto, CourseSettingEntity>();
        }

        private void MapsForUsers()
        {
            CreateMap<UserEntity, UserDto>();
            CreateMap<UserEditDto, UserEntity>();
        }

        private void MapsForTagsActivities()
        {
            CreateMap<TagActivityEntity, TagActivityDto>();
            CreateMap<TagActivityCreateDto, TagActivityEntity>();
            CreateMap<TagActivityEditDto, TagActivityEntity>();
        }

        // Mapeo del dashboard de cursos
        private void MapsForDashboardCourses()
        {
            CreateMap<ActivityEntity, DashboardCourseActivityDto>();
            CreateMap<StudentEntity, DashboardCourseStudentDto>()
                .ForMember(
                dest => dest.FullName,
                opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}") // Este mappeo es para concatenar firstName y lastName para asi obtener el nombre completo 
            );
        }
    }
}