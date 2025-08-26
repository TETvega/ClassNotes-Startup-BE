﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using ClassNotes.API.Database.Entities;

namespace ClassNotes.API.Database.Configuration
{
    public class ActivityConfiguration : IEntityTypeConfiguration<ActivityEntity>
    {
        public void Configure(EntityTypeBuilder<ActivityEntity> builder)
        {
            builder.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .HasPrincipalKey(e => e.Id);

            builder.HasOne(e => e.UpdatedByUser)
                .WithMany()
                .HasForeignKey(e => e.UpdatedBy)
                .HasPrincipalKey(e => e.Id);

            //Relación entre ActivityEntity y StudentActivityNoteEntity
            builder.HasMany(a => a.StudentNotes)
                .WithOne(san => san.Activity)
                .HasForeignKey(san => san.ActivityId)
                .OnDelete(DeleteBehavior.Restrict);

            //Relación entre ActivityEntity y UnitEntity
            builder.HasOne(a => a.Unit)
                .WithMany(c => c.Activities)
                .HasForeignKey(a => a.UnitId)
                .OnDelete(DeleteBehavior.Cascade);

            //Relacion entre ActivityEntity y TagEntity
            builder.HasOne(a => a.TagActivity)
                .WithMany()
                .HasForeignKey(a => a.TagActivityId)
                .HasPrincipalKey(a => a.Id)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}