using MyDigitalLibrary.Core.Entities;
using MyDigitalLibrary.Core.Models;

namespace MyDigialLibrary.Mappers
{
    public static class BookMapper
    {
        public static Book ToModel(this BookEntity e)
        {
            if (e == null) return null!;

            return new Book
            {
                Id = e.Id,
                UserId = e.UserId,
                Title = e.Title,
                Authors = e.Authors,
                Description = e.Description,
                OriginalFilename = e.OriginalFilename,
                FilePath = e.FilePath,
                FileSize = e.FileSize,
                MimeType = e.MimeType,
                CoverPath = e.CoverPath,
                FileId = e.FileId,
                CoverFileId = e.CoverFileId,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,

                // UI defaults; service layer or callers can set these based on user data
                Publisher = string.Empty,
                Rating = 0,
                Status = string.Empty,
                ProgressPercent = 0,
                Series = string.Empty,
                SeriesIndex = null,
                Tags = string.Empty
            };
        }

        public static BookEntity ToEntity(this Book model)
        {
            if (model == null) return null!;

            return new BookEntity
            {
                Id = model.Id,
                UserId = model.UserId,
                Title = model.Title,
                Authors = model.Authors,
                Description = model.Description,
                OriginalFilename = model.OriginalFilename ?? string.Empty,
                FilePath = model.FilePath ?? string.Empty,
                FileSize = model.FileSize,
                MimeType = model.MimeType,
                CoverPath = model.CoverPath,
                FileId = model.FileId,
                CoverFileId = model.CoverFileId,
                CreatedAt = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}