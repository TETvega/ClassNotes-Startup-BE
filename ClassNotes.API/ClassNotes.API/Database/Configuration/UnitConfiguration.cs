﻿using ClassNotes.API.Database.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace ClassNotes.API.Database.Configuration
{
	public class UnitConfiguration : IEntityTypeConfiguration<UnitEntity>
	{
		public void Configure(EntityTypeBuilder<UnitEntity> builder)
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