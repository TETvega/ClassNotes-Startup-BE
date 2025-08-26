﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ClassNotes.API.Database.Entities;
namespace ClassNotes.API.Database.Configuration
{
    public class StudentCourseConfiguration : IEntityTypeConfiguration<StudentCourseEntity>
    {
        public void Configure(EntityTypeBuilder<StudentCourseEntity> builder)
        {
            builder.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .HasPrincipalKey(e => e.Id);

            builder.HasOne(e => e.UpdatedByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdatedBy)
                .HasPrincipalKey(e => e.Id);
        }
    }
}